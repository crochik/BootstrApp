## Update Lead Finance

```
create procedure "UpdateLeadFinance"()
    language sql
BEGIN ATOMIC
 INSERT INTO "LeadFinance" ("LeadId", "EntityId", "CreatedOn", "LeadType", "PostalCode", "Organization", "FCI|IsPPA", "FCI|IsPPL", "HDYHAU", "Cost", "Invoiced", "Net", "ConvertedOn", "Appointment", "FirstAppointmentScheduled", "FisrtAppointment", "FirstScheduled", "FirstProduced", "TotalLanded", "AppointmentsCount")  SELECT "Lead"._id AS "LeadId",
             "Lead"."EntityId",
             "Lead"."CreatedOn",
             "LeadType"."Name" AS "LeadType",
             "Lead"."PostalCode",
             "Organization"."Name" AS "Organization",
             "Organization"."FCI|IsPPA",
             "Organization"."FCI|IsPPL",
             "SfLeadSource"."Name" AS "HDYHAU",
             COALESCE("Lead"."Properties|leadFee", (0)::numeric) AS "Cost",
             COALESCE("InvoiceLineItem"."Value", (0)::numeric) AS "Invoiced",
             (COALESCE("InvoiceLineItem"."Value", (0)::numeric) - COALESCE("Lead"."Properties|leadFee", (0)::numeric)) AS "Net",
             "Lead"."ConvertedOn",
                 CASE
                     WHEN ("Lead"."ConvertedOn" IS NULL) THEN 0
                     ELSE 1
                 END AS "Appointment",
             x."FirstAppointmentScheduled",
             x."FisrtAppointment",
             x."FirstScheduled",
             x."FirstProduced",
             COALESCE(x."TotalLanded", (0)::numeric) AS "TotalLanded",
             COALESCE(x."AppointmentsCount", (0)::bigint) AS "AppointmentsCount"
            FROM ((((("Lead"
              JOIN "LeadType" ON ((("Lead"."LeadTypeId")::text = ("LeadType"._id)::text)))
              JOIN "Organization" ON ((("Organization"._id)::text = ("Lead"."EntityId")::text)))
              LEFT JOIN "InvoiceLineItem" ON (((("InvoiceLineItem"."LeadId")::text = ("Lead"._id)::text) AND ((("InvoiceLineItem"."Rule")::text = 'PpaAppointment'::text) OR (("InvoiceLineItem"."BillItemId")::text = 'cffe361a-6dc4-44c4-890c-403816710ee4'::text)))))
              LEFT JOIN "SfLeadSource" ON ((("SfLeadSource"."ExternalId")::text = ("Lead"."Properties|hdyhau")::text)))
              LEFT JOIN ( SELECT "sf_WorkOrder"."LeadId",
                     min("sf_WorkOrder"."Milestones|AppointmentScheduled") AS "FirstAppointmentScheduled",
                     min("sf_WorkOrder"."Milestones|Appointment") AS "FisrtAppointment",
                     min("sf_WorkOrder"."Milestones|Scheduled") AS "FirstScheduled",
                     min("sf_WorkOrder"."Milestones|Produced") AS "FirstProduced",
                     sum("sf_WorkOrder"."Opportunity|Landed") AS "TotalLanded",
                     sum("sf_WorkOrder"."Metrics|AppointmentsCount") AS "AppointmentsCount"
                    FROM "sf_WorkOrder"
                   GROUP BY "sf_WorkOrder"."LeadId") x ON (((x."LeadId")::text = ("Lead"._id)::text)))
           WHERE ("Lead"."CreatedOn" >= '2023-01-01 00:00:00+00'::timestamp with time zone);
END;

alter procedure "UpdateLeadFinance"() owner to postgres;

```