export interface PaymentDto {
  id: number;
  bookingId: number;
  stripePaymentIntentId: string;
  amount: number;
  currency: string;
  status: string;
  paidAt?: string | null;
  createdAt: string;
}

export interface PaymentInitiateDto extends PaymentDto {
  clientSecret: string;
}

export interface InitiatePaymentRequest {
  bookingId: number;
  currency: string;
}

export interface ConfirmPaymentRequest {
  stripePaymentIntentId: string;
}
