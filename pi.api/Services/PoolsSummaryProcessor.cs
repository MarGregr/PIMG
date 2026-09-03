using Npgsql;

namespace pi.api.Services;

public class PoolsSummaryProcessor
{
    private readonly NpgsqlDataSource _dataSource;

    private class PointStatusRow
    {
        public int PointId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int Status { get; set; }
        public int Availability { get; set; }
        public DateTime Ts { get; set; }
    }

    private class ActivePoolInfo
    {
        public int PoolId { get; set; }
        public string PoolCode { get; set; } = string.Empty;
    }

    public PoolsSummaryProcessor(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    
    public async Task<int> RefreshSummaryAsync()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            //PointCode, PoolCode
            var pointsPoolMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string queryPointsPool = @"
                SELECT DISTINCT ON (p.code)
                    p.code,
                    pl.code AS pool_code
                FROM points p
                JOIN stations s ON s.id = p.station_id
                JOIN pools pl ON pl.id = s.pool_id
                ORDER BY p.code, s.ts DESC;";

            await using (var cmd = new NpgsqlCommand(queryPointsPool, conn, transaction))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string code = reader.GetString(0);
                    pointsPoolMap[code] = reader.GetString(1);
                }
            }


            string queryStatus = @"
                SELECT ds.point_id, p.code, ds.status, ds.availability, ds.ts
                FROM dynamic_status ds
                LEFT JOIN points p ON ds.point_id = p.id
                ORDER BY p.code, ds.ts;";

            var pointGroups = new Dictionary<string, List<PointStatusRow>>(StringComparer.OrdinalIgnoreCase);

            await using (var cmd = new NpgsqlCommand(queryStatus, conn, transaction))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string code = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (string.IsNullOrEmpty(code)) continue;

                    var row = new PointStatusRow
                    {
                        PointId = reader.GetInt32(0),
                        Code = code,
                        Status = reader.GetInt32(2),
                        Availability = reader.GetInt32(3),
                        Ts = reader.GetDateTime(4)
                    };

                    if (!pointGroups.TryGetValue(code, out var group))
                    {
                        group = new List<PointStatusRow>();
                        pointGroups[code] = group;
                    }
                    group.Add(row);
                }
            }


            var chargingDurations = new Dictionary<string, long>();
            var availabilityDurations = new Dictionary<string, long>();
            //TODO: Zmienić na datetime.now
            DateTime endTime = new DateTime(2026, 6, 28, 19, 0, 0, DateTimeKind.Utc);

            foreach (var (code, group) in pointGroups)
            {
                var sorted = group.OrderBy(x => x.Ts).ToList();

                int? prevStatus = null;
                DateTime? chargeStart = null;

                int prevAvaStatus = 0;
                DateTime? avaStart = null;

                long totalChargeSec = 0;
                long totalAvaSec = 0;

                foreach (var row in sorted)
                {
                    int status = row.Status;
                    int availability = row.Availability;
                    DateTime ts = row.Ts;

                    //Sesje ładowania
                    if (availability == 0)
                    {
                        prevStatus = 0;
                        chargeStart = null;
                    }
                    else
                    {
                        if (prevStatus == 1 && status == 0)
                        {
                            chargeStart = ts;
                        }
                        else if (prevStatus == 0 && status == 1 && chargeStart.HasValue)
                        {
                            long durationSec = (long)(ts - chargeStart.Value).TotalSeconds;
                            //Filtr maksymalny czas sesji 48h
                            if (durationSec <= 172800)
                            {
                                totalChargeSec += durationSec;
                            }
                            chargeStart = null;
                        }

                        prevStatus = status;
                    }

                    //Sesje dostępności
                    if (prevAvaStatus == 0 && availability == 1)
                    {
                        avaStart = ts;
                    }
                    else if (prevAvaStatus == 1 && availability == 0 && avaStart.HasValue)
                    {
                        long durationSec = (long)(ts - avaStart.Value).TotalSeconds;
                        if (durationSec != 0)
                        {
                            totalAvaSec += durationSec;
                        }
                        avaStart = null;
                    }

                    prevAvaStatus = availability;
                }

                if (avaStart.HasValue)
                {
                    long durationSec = (long)(endTime - avaStart.Value).TotalSeconds;
                    if (durationSec > 0)
                    {
                        totalAvaSec += durationSec;
                    }
                }

                chargingDurations[code] = totalChargeSec;
                availabilityDurations[code] = totalAvaSec;
            }


            var aggregatedStats = new Dictionary<string, (HashSet<string> Points, long ChargingTotal, long AvailabilityTotal)>(StringComparer.OrdinalIgnoreCase);

            var allPointCodes = chargingDurations.Keys.Union(availabilityDurations.Keys).Union(pointsPoolMap.Keys);

            foreach (var pointCode in allPointCodes)
            {
                if (!pointsPoolMap.TryGetValue(pointCode, out var poolCode)) continue;

                chargingDurations.TryGetValue(pointCode, out long cTotal);
                availabilityDurations.TryGetValue(pointCode, out long aTotal);

                if (!aggregatedStats.TryGetValue(poolCode, out var item))
                {
                    item = (new HashSet<string>(), 0L, 0L);
                }

                item.Points.Add(pointCode);
                item.ChargingTotal += cTotal;
                item.AvailabilityTotal += aTotal;
                aggregatedStats[poolCode] = item;
            }


            await new NpgsqlCommand("TRUNCATE TABLE pools_summary;", conn, transaction)
                .ExecuteNonQueryAsync();

            string insertSql = @"
                INSERT INTO pools_summary (
                    pool_code, pool_point_count,
                    charging_total, availability_total, usage_percentage
                ) VALUES (
                    @pool_code, @pool_point_count,
                    @charging_total, @availability_total, @usage_percentage
                );";

            int insertedRows = 0;

            foreach (var poolCode in aggregatedStats.Keys)
            {
                int poolPointCount = 0;
                long chargingTotal = 0L;
                long availabilityTotal = 0L;
                double usagePercentage = 0.0;

                if (aggregatedStats.TryGetValue(poolCode, out var stats))
                {
                    poolPointCount = stats.Points.Count;
                    chargingTotal = stats.ChargingTotal;
                    availabilityTotal = stats.AvailabilityTotal;

                    if (availabilityTotal > 0)
                    {
                        usagePercentage = Math.Round(((double)chargingTotal / availabilityTotal) * 100.0, 6);
                    }
                }

                await using var insertCmd = new NpgsqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("pool_code", poolCode);
                insertCmd.Parameters.AddWithValue("pool_point_count", poolPointCount);
                insertCmd.Parameters.AddWithValue("charging_total", chargingTotal);
                insertCmd.Parameters.AddWithValue("availability_total", availabilityTotal);
                insertCmd.Parameters.AddWithValue("usage_percentage", usagePercentage);

                await insertCmd.ExecuteNonQueryAsync();
                insertedRows++;
            }

            await transaction.CommitAsync();
            return insertedRows;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}