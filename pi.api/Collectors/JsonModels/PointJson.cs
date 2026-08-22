using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.JsonModels
{
    public class ChargingSolution
    {
        public int mode { get; set; }
        public int power { get; set; }
    }

    public class Connector
    {
        public List<int> interfaces { get; set; }
        public int mode { get; set; }
        public int power { get; set; }
        public bool cable_attached { get; set; }
        public string ts { get; set; }
    }

    public class SinglePointData
    {
        public long id { get; set; }
        public string code { get; set; }
        public long station_id { get; set; }
        public List<ChargingSolution> charging_solutions { get; set; }
        public List<Connector> connectors { get; set; }
        public string ts { get; set; }
    }

    public class PointJson
    {
        public List<SinglePointData> data { get; set; }
        public string generated { get; set; }
    }
}
