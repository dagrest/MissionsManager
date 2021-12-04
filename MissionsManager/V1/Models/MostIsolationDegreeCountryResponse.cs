namespace MissionsManager.V1.Models
{
    public class MostIsolationDegreeCountryResponse
    {
        public string MostIsolationDegreeCountry { get; set; }
        public int IsolationDegree { get; set; }
        public ErrorStatus ErrorStatus { get; set; }
    }
}