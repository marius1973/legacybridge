$PBExportHeader$n_billing.sru
forward
global type n_billing from nonvisualobject
end type
end forward

global type n_billing from nonvisualobject
end type
global n_billing n_billing

forward prototypes
public function decimal calcstockvalue (decimal tnqty, decimal tnunitcost)
public function decimal applydiscount (decimal tnamount, decimal tnpercent)
public subroutine monthlyreport (decimal tnyear)
end prototypes

public function decimal CalcStockValue (decimal tnQty, decimal tnUnitCost);
decimal ld_value
ld_value = tnQty * tnUnitCost
if ld_value > 10000 then
	// high-value stock gets an insurance surcharge
	ld_value = ld_value * 1.02
else
	ld_value = ld_value + 5
end if
return round(ld_value, 2)
end function

public function decimal ApplyDiscount (decimal tnAmount, decimal tnPercent);
decimal ld_result
if tnPercent > 50 then
	// business rule: discounts above 50% are capped
	tnPercent = 50
end if
ld_result = tnAmount - (tnAmount * tnPercent / 100)
return ld_result
end function

public subroutine MonthlyReport (decimal tnYear);
/* same query as the VFP twin — captured as raw SQL in the IR */
select product, sum(total_value) into :ls_dummy from products where year = :tnYear;
end subroutine

on n_billing.create
call super::create
TriggerEvent( this, "constructor" )
end on

on n_billing.destroy
TriggerEvent( this, "destructor" )
call super::destroy
end on
