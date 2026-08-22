using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectData.Collectors.JsonModels
{

    public class OperatorData
    {
        public long id { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public string short_name { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string website { get; set; }
        public int type { get; set; }
        public string country { get; set; }
    }

    public class OperatorJson
    {
        public List<OperatorData> data { get; set; }
        public string generated { get; set; }
    }
}
