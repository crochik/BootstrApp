# Emser
Interchange Control Version Number 00401

## REF*19
Value should be in REF3 based on document version 2.2.02 - MAY HAVE CHANGED IN 00401 (but then Shaw is wrong)

```REF*19*EMSER TILE LLC```

On version 00400 (2.2.02) should be on REF3 (not REF2). Of course it looks like an error in the spec as all the other REFs use REF2 for the value and even the Spec has examples with the value in REF2.

## Wrong SKU in Manufacturer Style Name and Code
```SLN*00001**O******SK*F45ACTIAD1939P***MG*F45ACTIAD1939P*ST*F45ACTIAD1939P*****MS*F45ACTIAD1939P*MN*F45ACTIAD1939P```

Why do we use the SKU as the manufacturer style name and manufacturer style number? Presumably in this case they should be "ACTION POLISHED 19X39"/"F01B49", no?

## Color Number
```
PID*F*73***AMARETTO
PID*F*35***F72ALPIAM0436
```

Uses SKU as color number.

## Escaping?
```PID*F*73***CAF�```

## Some colors override the SellingUnit from the style
???

## Assigned Identification is always 00001
```SLN*00001...```

Assigned Identification is always 00001, shouldn't it be unique?


# Shaw
Interchange Control Version Number 00401

## Invalid unit for Shipping Weight
```MEA**SW*5.64*SY```

# Daltile

## Missing required ""
LIN16 and LIN17
```LIN**GS*S0157F03390536SU0001*MF*DalTile*ST*0157***SZ*12x12*MS*S0157F03390536SU0001*MN*Onyx Collection Field Tile 12x12 Polished```

## Missing required MEA4: "Unit of Measurement Code"
```MEA**LN*13```
```MEA**WD*13```

## Missing required PID5: "Color Name"
```PID*F*73```

## Missing required PID5: "Color Number"
```PID*F*35```

```CTP****1*PC```

## PID 
TRN (at least) doesn't respect max of 80
```PID*F*TRN***Rittenhouse Square Field Tile 12x12 Mounted 2x4 BRICK JOINT MS Group 2 Glazed PK0.10```

## Invalid length: LIN15 (48)
City View Field Tile Mosaic Mounted 9x18 MS Group 1 Plain => 57
```LIN**GS*S0160F07121465SC00130003*MF*DalTile*ST*0160***SZ* Mounted 9x18 MS*MS*S0160F07121465SC00130003*MN*City View Field Tile Mosaic Mounted 9x18 MS Group 1 Plain```

## Invalid Unit Of measurement 

Invalid Element length for Unit of Measurement Code
```#19150: MEA**SU*1*FT2```

Invalid Element length for Unit of Measurement Code
```19151: CTP**LPR*5.21*1*FT2```

## Invalid Element length for Size Code
Invalid Element length for 'Size Code' (#10), expected <=48, got 57

```103158: LIN**GS*S0881F18914637SC00120000*MF*DalTile*ST*0881***SZ*RANDOM INTERLOCKING Mounted 11-3/4x14-5/8 INTERLOCKING MS*MS*S0881F18914637SC00120000*MN*Match Poin FLDTIL Mosaic RANDOM INTERLOCKING 11-3/4x14-5/8 INTERLOCKING MS UN```
