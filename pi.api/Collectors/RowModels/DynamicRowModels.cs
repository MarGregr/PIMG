using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.RowModels;

public record StatusRow
{
    public long PointId { get; set; }
    public int Availability { get; set; }
    public int Status { get; set; }
    public DateTime Ts { get; set; }
}

public record PriceRow
{
    public long PointId { get; set; }
    public string Unit { get; set; }
    public string Literal { get; set; }
    public int Price { get; set; }
    public DateTime Ts { get; set; }
}
