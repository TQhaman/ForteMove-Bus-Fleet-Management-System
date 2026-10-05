namespace ForteMove.Models.Fuel
{
    public enum FuelSupplyType { Diesel, ElectricCharging }
    public enum FuelQuantityUnit { Litres, KilowattHours }
    public enum FuelRequestStatus { Pending, Approved, Rejected, Cancelled }
    public enum FuelVoucherStatus { Active, Redeemed, Cancelled }
    public enum FuelVoucherDisplayState { Active, Expired, Redeemed, Cancelled }
}

