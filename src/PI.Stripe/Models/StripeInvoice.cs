using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace Models
{
    [BsonCollection("stripe.Invoice")]
    public class StripeInvoice : Model<string>
    {
        //
        // Summary:
        //     The date on which payment for this invoice is due. This value will be null for
        //     invoices where billing=charge_automatically.
        public DateTime? DueDate { get; set; }

        //
        // Summary:
        //     Ending customer balance after the invoice is finalized. Invoices are finalized
        //     approximately an hour after successful webhook delivery or when payment collection
        //     is attempted for the invoice. If the invoice has not been finalized yet, this
        //     will be null.
        public long? EndingBalance { get; set; }

        //
        // Summary:
        //     Footer displayed on the invoice.
        public string Footer { get; set; }

        //
        // Summary:
        //     The URL for the hosted invoice page, which allows customers to view and pay an
        //     invoice. If the invoice has not been finalized yet, this will be null.
        public string HostedInvoiceUrl { get; set; }

        //
        // Summary:
        //     The link to download the PDF for the invoice. If the invoice has not been finalized
        //     yet, this will be null.
        public string InvoicePdf { get; set; }

        //
        // Summary:
        //     The individual line items that make up the invoice.
        // public StripeList<InvoiceLineItem> Lines { get; set; }

        //
        // Summary:
        //     Set of key-value pairs that you can attach to an object. This can be useful for
        //     storing additional information about the object in a structured format.
        public Dictionary<string, string> Metadata { get; set; }

        //
        // Summary:
        //     The time at which payment will next be attempted. This value will be null for
        //     invoices where Stripe.Invoice.CollectionMethod is set to send_invoice.
        public DateTime? NextPaymentAttempt { get; set; }

        //
        // Summary:
        //     A unique, identifying string that appears on emails sent to the customer for
        //     this invoice.
        public string Number { get; set; }

        //
        // Summary:
        //     Whether payment was successfully collected for this invoice. An invoice can be
        //     paid (most commonly) with a charge or with credit from the customer's account
        //     balance.
        public bool Paid { get; set; }

        //
        // Summary:
        //     ID of the PaymentIntent associated with this invoice.
        public string PaymentIntentId { get; set; }

        //
        // Summary:
        //     End of the usage period during which invoice items were added to this invoice.
        public DateTime PeriodEnd { get; set; }

        //
        // Summary:
        //     Start of the usage period during which invoice items were added to this invoice.
        public DateTime PeriodStart { get; set; }

        //
        // Summary:
        //     Total amount of all post-payment credit notes issued for this invoice.
        public long PostPaymentCreditNotesAmount { get; set; }

        //
        // Summary:
        //     Total amount of all pre-payment credit notes issued for this invoice.
        public long PrePaymentCreditNotesAmount { get; set; }

        //
        // Summary:
        //     The aggregate amounts calculated per tax rate for all line items.
        // public List<InvoiceTaxAmount> TotalTaxAmounts { get; set; }

        //
        // Summary:
        //     Total after discounts and taxes.
        public long Total { get; set; }

        //
        // Summary:
        //     If Stripe.Invoice.BillingReason is set to subscription_threshold this returns
        //     more information on which threshold rules triggered the invoice.
        // public InvoiceThresholdReason ThresholdReason { get; set; }

        //
        // Summary:
        //     The amount of tax on this invoice. This is the sum of all the tax amounts on
        //     this invoice.
        public long? Tax { get; set; }

        //
        // Summary:
        //     Total of all subscriptions, invoice items, and prorations on the invoice before
        //     any discount or tax is applied.
        public long Subtotal { get; set; }

        //
        // Summary:
        //     Describes the current discount applied to this invoice, if there is one.
        // public Discount Discount { get; set; }

        //
        // Summary:
        //     Only set for upcoming invoices that preview prorations. The time used to calculate
        //     prorations.
        // public DateTime SubscriptionProrationDate { get; set; }

        //
        // Summary:
        //     ID of the subscription that this invoice was prepared for, if any.
        public string SubscriptionId { get; set; }

        //
        // Summary:
        //     The timestamps at which the invoice status was updated.
        // public InvoiceStatusTransitions StatusTransitions { get; set; }

        //
        // Summary:
        //     The status of the invoice, one of draft, open, paid, uncollectible, or void.
        public string Status { get; set; }

        //
        // Summary:
        //     Extra information about an invoice for the customer's credit card statement.
        public string StatementDescriptor { get; set; }

        //
        // Summary:
        //     Starting customer balance before the invoice is finalized. If the invoice has
        //     not been finalized yet, this will be the current customer balance.
        public long StartingBalance { get; set; }

        //
        // Summary:
        //     This is the transaction number that appears on email receipts sent for this invoice.
        public string ReceiptNumber { get; set; }

        //
        // Summary:
        //     If specified, the funds from the invoice will be transferred to the destination
        //     and the ID of the resulting transfer will be found on the invoice's charge.
        // public InvoiceTransferData TransferData { get; set; }

        //
        // Summary:
        //     An arbitrary string attached to the object. Often useful for displaying to users.
        //     Referenced as 'memo' in the Dashboard.
        public string Description { get; set; }

        //
        // Summary:
        //     Tax rates applied to the invoice.
        // public List<TaxRate> DefaultTaxRates { get; set; }

        //
        // Summary:
        //     The country of the business associated with this invoice, most often the business
        //     creating the invoice.
        // public string AccountCountry { get; set; }

        //
        // Summary:
        //     The public name of the business associated with this invoice, most often the
        //     business creating the invoice.
        public string AccountName { get; set; }

        //
        // Summary:
        //     Final amount due at this time for this invoice. If the invoice's total is smaller
        //     than the minimum charge amount, for example, or if there is account credit that
        //     can be applied to the invoice, the Stripe.Invoice.AmountDue may be 0. If there
        //     is a positive Stripe.Invoice.StartingBalance for the invoice (the customer owes
        //     money), the Stripe.Invoice.AmountDue will also take that into account. The charge
        //     that gets generated for the invoice will be for the amount specified in Stripe.Invoice.AmountDue.
        public long AmountDue { get; set; }

        //
        // Summary:
        //     The amount that was paid.
        public long AmountPaid { get; set; }

        //
        // Summary:
        //     The amount that was paid.
        public long AmountRemaining { get; set; }

        //
        // Summary:
        //     The fee in that will be applied to the invoice and transferred to the application
        //     owner's Stripe account when the invoice is paid.
        public long? ApplicationFeeAmount { get; set; }

        //
        // Summary:
        //     Number of payment attempts made for this invoice, from the perspective of the
        //     payment retry schedule. Any payment attempt counts as the first attempt, and
        //     subsequently only automatic retries increment the attempt count. In other words,
        //     manual payment attempts after the first attempt do not affect the retry schedule.
        public long AttemptCount { get; set; }

        //
        // Summary:
        //     Whether an attempt has been made to pay the invoice. An invoice is not attempted
        //     until 1 hour after the invoice.created webhook, for example, so you might not
        //     want to display that invoice as unpaid to your users.
        public bool Attempted { get; set; }

        //
        // Summary:
        //     Controls whether Stripe will perform automatic collection of the invoice. When
        //     false, the invoice's state will not automatically advance without an explicit
        //     action.
        public bool AutoAdvance { get; set; }

        //
        // Summary:
        //     Indicates the reason why the invoice was created. One of automatic_pending_invoice_item_invoice,
        //     manual, subscription, subscription_create, subscription_cycle, subscription_threshold,
        //     subscription_update, or upcoming.
        public string BillingReason { get; set; }

        //
        // Summary:
        //     ID of the latest charge generated for this invoice, if any.
        public string ChargeId { get; set; }

        //
        // Summary:
        //     Either charge_automatically, or send_invoice. When charging automatically, Stripe
        //     will attempt to pay this invoice using the default source attached to the customer.
        //     When sending an invoice, Stripe will email this invoice to the customer with
        //     payment instructions. Defaults to charge_automatically.
        public string CollectionMethod { get; set; }
        
        //
        // Summary:
        //     Three-letter ISO currency code, in lowercase. Must be a supported currency.
        public string Currency { get; set; }

        public string DefaultSourceId { get; set; }

        //
        // Summary:
        //     ID of the default payment method for the invoice. It must belong to the customer
        //     associated with the invoice and be in a chargeable state. If not set, defaults
        //     to the subscription’s default payment method, if any, or to the customer’s default
        //     payment method.
        public string DefaultPaymentMethodId { get; set; }

        //
        // Summary:
        //     The customer’s tax ids. Until the invoice is finalized, this field will equal
        //     Stripe.Customer.TaxIds. Once the invoice is finalized, this field will no longer
        //     be updated.
        // public List<InvoiceCustomerTaxId> CustomerTaxIds { get; set; }

        //
        // Summary:
        //     The customer’s tax exempt status. Until the invoice is finalized, this field
        //     will equal Stripe.Customer.TaxExempt. Once the invoice is finalized, this field
        //     will no longer be updated.
        public string CustomerTaxExempt { get; set; }

        //
        // Summary:
        //     Whether this object is deleted or not.
        public bool? Deleted { get; set; }

        //
        // Summary:
        //     The customer’s shipping information. Until the invoice is finalized, this field
        //     will equal Stripe.Customer.Shipping. Once the invoice is finalized, this field
        //     will no longer be updated.
        // public Shipping CustomerShipping { get; set; }

        //
        // Summary:
        //     The customer’s email. Until the invoice is finalized, this field will equal Stripe.Customer.Email.
        //     Once the invoice is finalized, this field will no longer be updated.
        public string CustomerEmail { get; set; }

        //
        // Summary:
        //     The customer’s address. Until the invoice is finalized, this field will equal
        //     Stripe.Customer.Address. Once the invoice is finalized, this field will no longer
        //     be updated.
        // public Address CustomerAddress { get; set; }

        //
        // Summary:
        //     The ID of the customer who will be billed.
        public string CustomerId { get; set; }

        //
        // Summary:
        //     Custom fields displayed on the invoice.
        // public List<InvoiceCustomField> CustomFields { get; set; }

        //
        // Summary:
        //     The customer’s phone number. Until the invoice is finalized, this field will
        //     equal Stripe.Customer.Phone. Once the invoice is finalized, this field will no
        //     longer be updated.
        public string CustomerPhone { get; set; }

        //
        // Summary:
        //     Invoices are automatically paid or sent 1 hour after webhooks are delivered,
        //     or until all webhook delivery attempts have been exhausted. This field tracks
        //     the time when webhooks for this invoice were successfully delivered. If the invoice
        //     had no webhooks to deliver, this will be set while the invoice is being created.
        public DateTime? WebhooksDeliveredAt { get; set; }
    }
}

