using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.JsonModels
{
    public class Status
    {
        public int availability { get; set; }
        public int status { get; set; }
        public string ts { get; set; }
    }

    public class Price
    {
        public string? price {  get; set; }
        public string unit { get; set; }
        public string literal { get; set; }
        public string ts { get; set; }
    }

    public class PointData
    {
        public long point_id { get; set; }
        public List<Price> prices { get; set; }
        public Status? status { get; set; }
    }

    public class DynamicJson
    {
        public List<PointData> data { get; set; }
        public string generated { get; set; }
    }
}
