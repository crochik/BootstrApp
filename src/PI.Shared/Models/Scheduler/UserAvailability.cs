using System;
using System.Collections.Generic;

namespace PI.Shared.Models
{
    // [BsonCollection("UserAvailability")]
    public class UserAvailability : EntityOwnedModel
    {
        public Guid? OrganizationId { get; set; }
        public Dictionary<string, TimeBlockStats> Stats { get; set; } = new Dictionary<string, TimeBlockStats>();
        public string TimeZoneId { get; set; }
        public int Duration { get; set; }
        public XTimeSlot[] Slots { get; set; }

        public void CalculateStats()
        {
            foreach (var slot in Slots)
            {
                var key = slot.Tag ?? "Other";
                if (!Stats.TryGetValue(key, out var block))
                {
                    block = new TimeBlockStats
                    {
                        Name = key,
                    };

                    Stats.Add(key, block);
                }

                int count = (int)((slot.End - slot.Start).TotalMinutes / Duration);

                block.Count += count;
                if (!block.FirstDate.HasValue || block.FirstDate > slot.Start) block.FirstDate = slot.Start;
            }
        }
    }
}