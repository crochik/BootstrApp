using System;
using FluentAssertions;
using Messages.Flow;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using Xunit;

namespace UnitTests
{
    public class SimpleActionMessageTest
    {
        [Fact]
        public void PostToSlackAction_PaymentStatusUpdateEvent()
        {
            var entity = new Organization
            {
                AccountId = AccountIds.CSS,
                EntityId = AccountIds.CSS,
                Name = "Test",
                Id = Guid.NewGuid()
            };
            
            //Given
            var evt = new PaymentStatusUpdateEvent(entity)
            {
                Payment = new Payment 
                {
                    Id = Guid.Empty,
                    ExternalId = "test"
                },
                Status = PaymentStatus.Failed,
            };

            var options = new PostToSlackActionOptions
            {

            };
            
            var message = new PostToSlackAction.Message(evt, options);

            //When
            var json = JsonConvert.SerializeObject(message);
            var obj = JsonConvert.DeserializeObject<PostToSlackAction.Message>(json);

            //Then
            obj.Should().NotBeNull();
            obj.Event.GetType().Should().Be(typeof(PaymentStatusUpdateEvent));
            (obj.Event as PaymentStatusUpdateEvent).Payment.Id.Should().Be(Guid.Empty);
            (obj.Event as PaymentStatusUpdateEvent).Payment.ExternalId.Should().Be("test");
            (obj.Event as PaymentStatusUpdateEvent).Status.Should().Be(PaymentStatus.Failed);
        }

    }
}
