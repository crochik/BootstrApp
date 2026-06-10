*      ExternalId: $INET_External_Id__c
*      INET_Branch__c: "{{{Parameters.SfBranchId}}}"
*      INET_External_Id__c: 1
*      INET_Margin__c:
      INET_Price2__c: 1
      Pricebook2Id: "{{{Parameters.SfPricebookId}}}"
      Product2Id: $ExternalId
      INET_FCIUID__c: $INET_External_Id__c
      UnitPrice:
      UseStandardPrice: 1
      IsActive: 1

      INET_Notes__c: "{{{Parameters.SfSupplierId}}}"

      Description: $Description
      INET_Cost1__c:
      INET_Cost2__c:
-      INET_Supplier__c: "{{{Parameters.SfSupplierId}}}"
-      ProductCode: $SKU

      INET_PackagesPerPallet__c: $PackagesPerPallet.Units
-      INET_RollLength__c: $RollLength
-     INET_RollWidth__c: $RollWidth
      INET_UnitsPerPackage__c: $Package.Units
-      QuantityUnitOfMeasure:

      INET_ProductID__c: $Product.INET_ProductID__c
      INET_ProductSetting__c: $Product.INET_ProductSetting__c
      INET_ProductType__c: $Product.INET_ProductType__c
      INET_IsLabor__c: 1
      INET_IsRoll__c: $Product.INET_IsRoll__c
