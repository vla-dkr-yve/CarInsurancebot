using TTBot.MindeeData;

namespace TTBot.Session
{
    public class UserSession
    {
        public PassportData? PassportInfo { get; set; }
        public VehicleData? VehicleInfo { get; set; }
        public bool IsPassportConfirmed { get; set; } = false;
        public bool IsVehicleConfirmed { get; set; } = false;
        public bool IsPaymentConfirmed { get; set; } = false;
    }
}
