using System;
using System.Collections.Generic;
using Crochik.Data;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models
{
    public class Search : IQueryParams
    {
        public Condition[] Criteria { get; set; }
        public int Top { get; set; }
        public string OrderBy { get; set; }
        public int Skip { get; set; }
    }

    public class SearchResults<T>
    {
        public Search Search { get; set; }
        public IEnumerable<T> Results { get; set; }
    }

    public class LeadSearchResults : SearchResults<LeadSearchResult>
    {
    }

    public class LeadSearchResult
    {
        public Guid Id { get; set; }
        public Guid EntityId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public Guid? AssignedEntityId { get; set; }
        public Guid? LeadStatusId { get; set; }
        public DateTime CreatedOn { get; set; }
        // public DateTime? LastUpdatedOn { get; set; }
        public Guid LeadTypeId { get; set; }
    }
}
