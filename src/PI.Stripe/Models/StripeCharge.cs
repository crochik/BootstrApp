using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace Models;

public class StripeChargeOutcome
{
    //
    // Summary:
    //     Possible values are approved_by_network, declined_by_network, not_sent_to_network,
    //     and reversed_after_approval. The value reversed_after_approval indicates the
    //     payment was blocked by Stripe after bank authorization, and may temporarily appear
    //     as “pending” on a cardholder’s statement.
    public string NetworkStatus { get; set; }

    //
    // Summary:
    //     An enumerated value indicating a more detailed explanation of the outcome’s type.
    //     See understanding declines for details.
    public string Reason { get; set; }

    //
    // Summary:
    //     An enumerated value indicating a more detailed explanation of the outcome’s type.
    //     See understanding declines for details.
    public string RiskLevel { get; set; }

    //
    // Summary:
    //     Stripe’s evaluation of the riskiness of the payment. Possible values for evaluated
    //     payments are between 0 and 100.
    public long RiskScore { get; set; }

    //
    // Summary:
    //     The ID of the Radar rule that matched the payment, if applicable.
    public string RuleId { get; set; }

    //
    // Summary:
    //     A human-readable description of the outcome type and reason, designed for you
    //     (the recipient of the payment), not your customer.
    public string SellerMessage { get; set; }

    //
    // Summary:
    //     Possible values are authorized, issuer_declined, blocked, and invalid. See understanding
    //     declines for details.
    public string Type { get; set; }
}

public class StripeChargeDetails
{
    //
    // Summary:
    //     Unique identifier for the object.
    public string Id { get; set; }

    //
    // Summary:
    //     A set of key/value pairs that you can attach to a charge object. It can be useful
    //     for storing additional information about the charge in a structured format.
    public Dictionary<string, string> Metadata { get; set; }

    //
    // Summary:
    //     The account (if any) the charge was made on behalf of without triggering an automatic
    //     transfer. See the Connect documentation for details.
    public string OnBehalfOfId { get; set; }
    //
    // Summary:
    //     ID of the order this charge is for if one exists.
    public string OrderId { get; set; }

    //
    // Summary:
    //     Details about whether the payment was accepted, and why.
    public StripeChargeOutcome Outcome { get; set; }

    //
    // Summary:
    //     true if the charge succeeded, or was successfully authorized for later capture.
    public bool Paid { get; set; }

    //
    // Summary:
    //     ID of the payment intent this charge is for if one exists.
    public string PaymentIntentId { get; set; }

    //
    // Summary:
    //     ID of the PaymentMethod associated with this charge.
    public string PaymentMethod { get; set; }

    //
    // Summary:
    //     Transaction-specific details of the payment method used in the payment.
    // public ChargePaymentMethodDetails PaymentMethodDetails { get; set; }

    //
    // Summary:
    //     This is the email address that the receipt for this charge was sent to.
    public string ReceiptEmail { get; set; }

    //
    // Summary:
    //     This is the transaction number that appears on email receipts sent for this charge.
    public string ReceiptNumber { get; set; }

    //
    // Summary:
    //     This is the URL to view the receipt for this charge. The receipt is kept up-to-date
    //     to the latest state of the charge, including any refunds. If the charge is for
    //     an Invoice, the receipt will be stylized as an Invoice receipt.
    public string ReceiptUrl { get; set; }

    //
    // Summary:
    //     Whether or not the charge has been fully refunded. If the charge is only partially
    //     refunded, this attribute will still be false.
    public bool Refunded { get; set; }

    //
    // Summary:
    //     A list of refunds that have been applied to the charge.
    // public StripeList<Refund> Refunds { get; set; }

    //
    // Summary:
    //     ID of the review associated with this charge if one exists.
    public string ReviewId { get; set; }

    //
    // Summary:
    //     Shipping information for the charge.
    // public Shipping Shipping { get; set; }

    //
    // Summary:
    //     For most Stripe users, the source of every charge is a credit or debit card.
    //     This hash is then the card object describing that card.
    // public IPaymentSource Source { get; set; }

    //
    // Summary:
    //     The transfer ID which created this charge. Only present if the charge came from
    //     another Stripe account. See the Connect documentation for details.
    public string SourceTransferId { get; set; }

    //
    // Summary:
    //     Extra information about a charge. This will appear on your customer’s credit
    //     card statement.
    public string StatementDescriptor { get; set; }

    //
    // Summary:
    //     Provides information about the charge that customers see on their statements.
    //     Concatenated with the prefix (shortened descriptor) or statement descriptor that’s
    //     set on the account to form the complete statement descriptor. Maximum 22 characters
    //     for the concatenated descriptor.
    public string StatementDescriptorSuffix { get; set; }

    //
    // Summary:
    //     The status of the payment is either succeeded, pending, or failed.
    public string Status { get; set; }

    //
    // Summary:
    //     ID of the transfer to the destination account (only applicable if the charge
    //     was created using the destination parameter).
    public string TransferId { get; set; }

    //
    // Summary:
    //     Details about the level III data associated with the Charge. This is a gated
    //     property and most integrations can not access it.
    // public ChargeLevel3 Level3 { get; set; }

    //
    // Summary:
    //     ID of the invoice this charge is for if one exists.
    public string InvoiceId { get; set; }

    //
    // Summary:
    //     A positive integer in the smallest currency unit (e.g., 100 cents to charge $1.00
    //     or 100 to charge ¥100, a 0-decimal currency) representing how much to charge.
    //     The minimum amount is $0.50 US or equivalent in charge currency.
    public long Amount { get; set; }

    //
    // Summary:
    //     Amount in cents refunded (can be less than the amount attribute on the charge
    //     if a partial refund was issued).
    public long AmountRefunded { get; set; }

    public string ApplicationId { get; set; }

    public string ApplicationFeeId { get; set; }

    //
    // Summary:
    //     The amount of the application application fee (if any) for the charge. See the
    //     Connect documentation for details.
    public long? ApplicationFeeAmount { get; set; }

    //
    // Summary:
    //     ID of the balance transaction that describes the impact of this charge on your
    //     account balance (not including refunds or disputes).
    public string BalanceTransactionId { get; set; }

    //
    // Summary:
    //     Billing details of the payment method used in the payment.
    // public BillingDetails BillingDetails { get; set; }

    // public ChargeTransferData TransferData { get; set; }

    public DateTime Created { get; set; }

    //
    // Summary:
    //     The full statement descriptor that is passed to card networks, and that is displayed
    //     on your customers' credit card and bank statements. Allows you to see what the
    //     statement descriptor looks like after the static and dynamic portions are combined.
    public string CalculatedStatementDescriptor { get; set; }

    //
    // Summary:
    //     Three-letter ISO currency code representing the currency in which the charge
    //     was made.
    public string Currency { get; set; }

    //
    // Summary:
    //     ID of the customer this charge is for if one exists.
    public string CustomerId { get; set; }

    public string Description { get; set; }

    public string DestinationId { get; set; }

    public string DisputeId { get; set; }

    //
    // Summary:
    //     Whether the charge has been disputed. More than one dispute may exist on this
    //     charge.
    public bool Disputed { get; set; }

    //
    // Summary:
    //     Error code explaining reason for charge failure if available (see the errors
    //     section for a list of codes).
    public string FailureCode { get; set; }

    //
    // Summary:
    //     Message to user further explaining reason for charge failure if available.
    public string FailureMessage { get; set; }

    //
    // Summary:
    //     Information on fraud assessments for the charge.
    // public ChargeFraudDetails FraudDetails { get; set; }

    //
    // Summary:
    //     If the charge was created without capturing, this boolean represents whether
    //     or not it is still uncaptured or has since been captured.
    public bool Captured { get; set; }

    //
    // Summary:
    //     A string that identifies this transaction as part of a group. See the Connect
    //     documentation for details.
    public string TransferGroup { get; set; }
}

[BsonCollection("stripe.Charge")]
public class StripeCharge : Model
{
    [BsonElement("ExternalId")]
    public string ExternalId => Details?.Id;
        
    public StripeChargeDetails Details { get; set; }
}