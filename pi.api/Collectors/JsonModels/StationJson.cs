using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.JsonModels
{
    public class Location
    {
        public string? province { get; set; }
        public string? district { get; set; }
        public string? community { get; set; }
        public string? city { get; set; }
    }

    public class AuthenticationMethod
    {
        public int authentication_method { get; set; }
    }

    public class PaymentMethod
    {
        public int payment_method { get; set; }
    }

    public class StationData
    {
        public long id { get; set; }
        public long pool_id { get; set; }
        public string type { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public Location location { get; set; }
        public string ts { get; set; }
        public List<int> authentication_methods { get; set; } 
        public List<int> payment_methods { get; set; }        
    }

    public class StationJson
    {
        public List<StationData> data { get; set; }
        public string generated { get; set; }
    }
}
