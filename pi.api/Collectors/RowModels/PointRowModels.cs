using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.RowModels;

public record PointRow
{
    public long Id { get; set; }
    public string Code { get; set; }
    public long StationId { get; set; }
    public DateTime Ts { get; set; }
}

public record ChargingSolutionRow
{
    public long PointId { get; set; }
    public int Mode { get; set; }
    public int Power { get; set; }
}

public record ConnectorRow
{
    public long PointId { get; set; }
    public int Power { get; set; }
    public bool CableAttached { get; set; }

    public int[] Interfaces { get; set; } = [];

    public DateTime Ts { get; set; }
}

//protected record ConnectorInterfaceRow
//{
//    public long ConnectorId { get; set; }
//    public int Interface { get; set; }
//}
