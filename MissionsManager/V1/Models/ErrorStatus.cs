namespace MissionsManager.V1.Models
{
    public class ErrorStatus
    {
        public ErrorType ErrorType { get; set; }
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
    }
}