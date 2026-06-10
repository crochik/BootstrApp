using System;

namespace Qvinci.Models
{
    public enum QvinciReport
    {
        Aging,
        AP,
        BalanceSheet_LastYear,
        BalanceSheet_YTD,
        PNL_LastYear,
        PNL_YTD,
    }

    public class ReportSelectedOptions
    {
        public string DateFrequency { get; set; } //  "by Month",
        public bool UseAccountMapping { get; set; }// true,
        public string VerticalAnalysisType { get; set; } // "None",
        public bool IncludeComputedColumns { get; set; }// true,
        public string RelativeDateRange { get; set; } // "Last Calendar Year",
        public bool UseCustomDateRange { get; set; } //
        public DateTime? StartDate { get; set; } //
        public DateTime? EndDate { get; set; } //
        public string[] Filters { get; set; } //
        public int[] Locations { get; set; }
    }

    public class ReportValue
    {
        public decimal Value { get; set; }
        public int Level { get; set; }
        public int ColumnLoc { get; set; }
        public string RowName { get; set; } // "LIABILITIES & EQUITY",
        public string FullName { get; set; } // "LIABILITIES & EQUITY",
        public string ColumnName { get; set; }
        public bool IsTotal { get; set; }
    }

    public class ReportSection
    {
        public string Name { get; set; }
        public ReportValue[] Values { get; set; }
        public int Level { get; set; }
        public int ColumnCount { get; set; }
        public bool IsParentAccount { get; set; }
        public ReportSection[] Children { get; set; }
        public ReportSection TotalRow { get; set; }
    }

    public class ReportModel
    {
        public string ReportHeader { get; set; } // "Balance Sheet",
        public int BuildTime { get; set; }//  0,
        public ReportSection[] TopMostRows { get; set; }
        public string[] ColumnNames { get; set; }
    }

    public class HttpClientRequestException : Exception
    {
        public HttpClientRequestException(string message) : base(message)
        {
        }

        public int StatusCode { get; set; }
        public string Status { get; set; }
        public string Body { get; set; }
        public string Url { get; set; }
    }

    public class ReportFile
    {
        public string Url { get; set; }
        public ReportSelectedOptions SelectedOptions { get; set; }
        public ReportModel ReportModel { get; set; }
    }
}
