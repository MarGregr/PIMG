using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.RowModels;

public record StationRow
{
    public long Id { get; set; }
    public long PoolId { get; set; }
    public string Type { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Province { get; set; }
    public string District { get; set; }
    public string Community { get; set; }
    public string City { get; set; }
    public DateTime Ts { get; set; }
}

public record AuthenticationMethodRow
{
    public long StationId { get; set; }
    public int AuthenticationMethod { get; set; }
}

public record PaymentMethodRow
{
    public long StationId { get; set; }
    public int PaymentMethod { get; set; }
}
