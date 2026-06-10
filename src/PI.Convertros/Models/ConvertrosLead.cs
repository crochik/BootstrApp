using System;

namespace Models;

public class ConvertrosLead
{
    #region Header Information

    public Guid? Id { get; set; }

    public string LeadState { get; set; }

    public DateTime DateUTC { get; set; }

    public DateTime LastUpdateUTC { get; set; }

    #endregion

    #region Lead Specifics

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string LeadName { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string FirstName { get; set; }


    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string LastName { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string EMail { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string CompanyName { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Phone { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Phone2 { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string PhoneMatch { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Phone2Match { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string LocationNumber { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string LocationName { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Addres_Line1 { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Addres_Line2 { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Addres_City { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Addres_PostalCode { get; set; }
    public string Addres_State { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string Notes { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    public string LeadLink { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string Tags { get; set; }


    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string Source { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string SourceCampaign { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string SourceInternalId { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string SourceKeywords { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string Authorization { get; set; }

    // [DisplayFormat(ConvertEmptyStringToNull = true)]
    // [MaxLength(250)]
    public string SchedulerURL { get; set; }

    #endregion
}
