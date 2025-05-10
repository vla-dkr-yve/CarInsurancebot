using TTBot.MindeeData;

namespace TTBot.Session
{
    public class UserSession
    {
        public PassportData PassportInfo { get; set; } = new PassportData();
        public VehicleData VehicleInfo { get; set; } = new VehicleData();
        public bool IsPassportConfirmed { get; set; } = false;
        public bool IsVehicleConfirmed { get; set; } = false;
        public bool IsPaymentConfirmed { get; set; } = false;
    }
}
