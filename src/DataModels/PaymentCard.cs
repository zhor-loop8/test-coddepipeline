namespace WebAPI.DataModels
{
    public class PaymentCard
    {
        public string Number { get; set; }
        public string Cvc { get; set; }
        public long? ExpMonth { get; set; }
        public long? ExpYear { get; set; }
    }
}
