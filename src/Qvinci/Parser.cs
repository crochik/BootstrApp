using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Qvinci.Models;

namespace Qvinci
{
    public class Parser
    {
        private readonly XmlDocument _document;
        private readonly XmlNamespaceManager _namespaceManager;
        private readonly string _location;
        private readonly QvinciReport _report;

        public static List<KPIRow> Parse(string locationName, QvinciReport report, XmlDocument doc)
        {
            return new Parser(locationName, report, doc).ParseDoc();
        }

        private Parser(string locationName, QvinciReport report, XmlDocument doc)
        {
            this._location = locationName;
            this._report = report;
            this._document = doc;

            this._namespaceManager = new XmlNamespaceManager(doc.NameTable);
            this._namespaceManager.AddNamespace("d2p1", "http://schemas.datacontract.org/2004/07/QvinciAPI.ServiceModels.Reporting");
        }

        public List<KPIRow> ParseDoc()
        {
            var kpis = new List<KPIRow>();
            var nodes = _document.SelectNodes("//d2p1:TopMostRows/d2p1:ReportRowResponseModel", _namespaceManager);

            if (nodes == null) return kpis;

            foreach (XmlNode root in nodes)
            {
                Parse(kpis, root, null);
            }

            return kpis;
        }

        public void Parse(List<KPIRow> kpis, XmlNode node, KPIRow parent)
        {
            var kpi = Parse(node, parent);
            kpis.Add(kpi);

            var children = node.SelectNodes("d2p1:Children/d2p1:ReportRowResponseModel", _namespaceManager);
            if (children == null) return;

            foreach (XmlNode child in children)
            {
                Parse(kpis, child, kpi);
            }
        }

        public KPIRow Parse(XmlNode node, KPIRow parent)
        {
            var name = node.SelectSingleNode("d2p1:Name", _namespaceManager) == null ?
                string.Empty :
                node.SelectSingleNode("d2p1:Name", _namespaceManager).InnerText;

            var lvl = node.SelectSingleNode("d2p1:Level", _namespaceManager) == null ?
                0 :
                int.Parse(node.SelectSingleNode("d2p1:Level", _namespaceManager).InnerText);

            var kpiValues = new List<KPIValue>();
            var vals = node.SelectNodes("d2p1:Values/d2p1:ReportRowResponseModel.ValueViewModel", _namespaceManager);
            if (vals != null)
            {
                foreach (XmlNode v in vals)
                {
                    string month = v.SelectSingleNode("d2p1:ColumnName", _namespaceManager)?.InnerText;

                    if (string.IsNullOrWhiteSpace(month) || string.Equals(month, "total", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string dval = v.SelectSingleNode("d2p1:Value", _namespaceManager) == null ?
                        "0" :
                        v.SelectSingleNode("d2p1:Value", _namespaceManager).InnerText;

                    var value = _report switch
                    {
                        QvinciReport.Aging => AgingValue(month, dval),
                        QvinciReport.AP => AgingValue(month, dval),
                        QvinciReport.BalanceSheet_LastYear => DataRangeKPI(month, dval),
                        QvinciReport.BalanceSheet_YTD => DataRangeKPI(month, dval),
                        QvinciReport.PNL_LastYear => DataRangeKPI(month, dval),
                        QvinciReport.PNL_YTD => DataRangeKPI(month, dval),
                        _ => throw new NotImplementedException()
                    };

                    kpiValues.Add(value);
                }
            }

            return new KPIRow
            {
                Id = Guid.NewGuid(),
                Name = name,
                Level = lvl,
                ParentId = parent?.Id,
                Path = parent?.Path != null ? parent.Path + ">" + name : name,
                Values = kpiValues.ToDictionary(x => x.Key ?? x.Column)
            };
        }

        private KPIValue AgingValue(string period, string value)
        {
            // (int?, int?) range = period switch
            // {
            //     "Current" => (default(int?), 0),
            //     "Over 90" => (91, default(int?)),
            //     "1 - 30" => (1, 30),
            //     "31 - 60" => (31, 60),
            //     "61 - 90" => (61, 90),
            //     _ => throw new Exception("Unexpected")
            // };

            var key = period switch
            {
                "Current" => "Current",
                "1 - 30" => "Month1",
                "31 - 60" => "Month2",
                "61 - 90" => "Month3",
                "Over 90" => "Over90",
                _ => throw new Exception("Unexpected")
            };

            return new KPIValue
            {
                Key = key,
                Column = period,
                Value = Convert.ToDouble(value),
                // StartDays = range.Item1,
                // EndDays = range.Item2
            };
        }

        private KPIValue DataRangeKPI(string period, string value)
        {
            if (!DateTime.TryParse(period, out var start))
            {
                throw new Exception("Unexpected format");
            }

            return new KPIValue
            {
                Column = period,
                Value = Convert.ToDouble(value),
                Month = start.Month,
                Year = start.Year,
                Key = start.Month.ToString(),
                // StartDate = start,
                // EndDate = start.AddMonths(1).AddDays(-1)
            };
        }
    }
}
