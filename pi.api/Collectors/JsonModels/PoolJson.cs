using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CollectData.Collectors.JsonModels
{
    public class PoolsOperatingHour
    {
        public int id { get; set; }
        public int pool_id { get; set; }
        public int weekday { get; set; }
        public string from_time { get; set; }
        public string to_time { get; set; }
    }

    public class PoolsClosingHour
    {
        public int id { get; set; }
        public int pool_id { get; set; }
        public int weekday { get; set; }
        public string from_time { get; set; }
        public string to_time { get; set; }
    }

    public class PoolData
    {
        public long id { get; set; }
        public long operator_id { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public string accesibility { get; set; }
        public bool charging { get; set; }
        public bool filling { get; set; }
        public int elevation { get; set; }
        public string street { get; set; }
        public string house_number { get; set; }
        public string house_number_addition { get; set; }
        public string postal_code { get; set; }
        public string city { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string operator_name { get; set; }
        public string operator_phone { get; set; }
        public string operator_website { get; set; }
        public string operator_email { get; set; }
        public string ts { get; set; }
        public string? teryt { get; set; }
        public JsonNode features { get; set; }
        public List<PoolsOperatingHour> operating_hours { get; set; }
        public List<PoolsClosingHour> closing_hours { get; set; }
    }

    public class PoolJson
    {
        public List<PoolData> data { get; set; }
        public string generated { get; set; }
    }
}
