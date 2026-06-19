namespace EMSModelLibrary.DTOs
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string StripePaymentIntentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentInitiateDto : PaymentDto
    {
        public string ClientSecret { get; set; } = string.Empty;
    }

    public class InitiatePaymentRequest
    {
        public int BookingId { get; set; }
        public string Currency { get; set; } = "inr";
    }

    public class ConfirmPaymentRequest
    {
        public string StripePaymentIntentId { get; set; } = string.Empty;
    }
}
