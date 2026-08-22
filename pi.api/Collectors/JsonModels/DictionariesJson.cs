public class DictionaryData
{
    public List<IdNameModel> charging_mode { get; set; }
    public List<IdNameDescModel> connector_interface { get; set; }
    public List<IdNameDescModel> fuel_type { get; set; }
    public List<IdNameModel> gas_connector_interface { get; set; }
    public List<IdNameDescModel> hydrogen_refill_solution { get; set; }
    public List<IdDescModel> station_authentication_method { get; set; }
    public List<IdDescModel> station_payment_method { get; set; }
    public List<IdNameModel> weekday { get; set; }
    public List<IdNameModel> company_type { get; set; }
    public List<IdStringNameModel> country { get; set; }
}

public class IdNameModel
{
    public int id { get; set; }
    public string name { get; set; }
}

public class IdDescModel
{
    public int id { get; set; }
    public string description { get; set; }
}

public class IdNameDescModel
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
}

public class IdStringNameModel
{
    public string id { get; set; }
    public string name { get; set; }
}