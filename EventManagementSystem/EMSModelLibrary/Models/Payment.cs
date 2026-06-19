namespace EMSModelLibrary.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string StripePaymentIntentId { get; set; } = string.Empty;
        public string StripeChargeId { get; set; } = string.Empty;
        public string StripeCustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public Payment()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public Payment(int id, int bookingId, string stripePaymentIntentId, string stripeChargeId, string stripeCustomerId, decimal amount, string currency, string status, DateTime paidAt)
        {
            Id = id;
            BookingId = bookingId;
            StripePaymentIntentId = stripePaymentIntentId;
            StripeChargeId = stripeChargeId;
            StripeCustomerId = stripeCustomerId;
            Amount = amount;
            Currency = currency;
            Status = status;
            PaidAt = paidAt;
            CreatedAt = DateTime.UtcNow;
        }
    }
}