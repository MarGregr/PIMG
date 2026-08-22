using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.RowModels;

public record PoolRow
{
    public long Id { get; set; }
    public long OperatorId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Accesibility { get; set; }
    public bool? Charging { get; set; }
    public bool? Filling { get; set; }
    public int? Elevation { get; set; }
    public string Street { get; set; }
    public string HouseNumber { get; set; }
    public string HouseNumberAddition { get; set; }
    public string PostalCode { get; set; }
    public string City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string OperatorName { get; set; }
    public string OperatorPhone { get; set; }
    public string OperatorWebsite { get; set; }
    public string OperatorEmail { get; set; }
    public DateTime Ts { get; set; }
    public string? Teryt { get; set; }
}

public record FeatureRow
{
    public long PoolId { get; set; }
    public string Feature { get; set; }
}

public record OperatingHourRow
{
    public long PoolId { get; set; }
    public int Weekday { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
}

public record ClosingHourRow
{
    public long PoolId { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
}