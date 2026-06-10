using System;
using Crochik.Messaging;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace Models
{
    [BsonCollection("ime.notification")]
    public class Notification : IMessageBody
    {
        [BsonId]
        public Guid Id { get; set; } = Model.NewObjectId();
        public Guid AccountId { get; set; }
        public NotificationPayload Payload { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class NotificationPayload
    {
        public int WorkOrderId { get; set; }
        public int AffiliateId { get; set; }

        // DOCUMENT_SIGNED
        // STATUS_CHANGED
        // CHANGE_ORDER
        public string Type { get; set; }

        // --DOCUMENT_SIGNED
        // SALE_CONTRACT
        // COMPLETION_CERTIFICATE
        // NOTICE_OF_RESCISSION
        // CHANGE_ORDER
        // -- STATUS_CHANGED
        // APPOINTMENT_PENDING
        // APPOINTMENT_SET
        // APPOINTMENT_RUN
        // SALE
        // ORDER_INITIATED
        // ITEMS_ORDERED
        // PRODUCT_SHIPPED
        // INSTALL_PENDING
        // COMPLETED
        // COMPLETED_PAID
        // CANCELLED
        // INSTALL_SCHEDULED
        // SALE_PENDING
        // SIT_NO_SALE
        // CLOSED
        // CLOSED_PENDING
        // SIT_NO_SALE_PENDING
        // CANCELLED_PENDING
        // PRODUCT_RECEIVED
        // PRODUCT_DELIVERED
        // MEASUREMENT_APPOINTMENT_SET
        // MEASUREMENT_APPOINTMENT_RUN
        // -- CHANGE_ORDER
        // <null> ???
        public string DocumentType { get; set; }

        public DateTime ChangedDate { get; set; }
    }

    public static class NotificationExtensions
    {
        public static string BuildRoute(this Notification notification) 
            => $"ime.{notification.Payload.AffiliateId}.{notification.Payload.Type}";
    }
}