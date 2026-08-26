* =====================================================
* inv_calc.prg — legacy inventory valuation (VFP)
* Realistic sample used by the CI pipeline and evals.
* =====================================================

PROCEDURE CalcStockValue
    LPARAMETERS tnQty, tnUnitCost
    LOCAL lnValue
    lnValue = tnQty * tnUnitCost
    IF lnValue > 10000
        * high-value stock gets an insurance surcharge
        lnValue = lnValue * 1.02
    ELSE
        lnValue = lnValue + 5
    ENDIF
    RETURN ROUND(lnValue, 2)
ENDPROC

FUNCTION ApplyDiscount
    LPARAMETERS tnAmount, tnPercent
    LOCAL lnResult
    IF tnPercent > 50
        * business rule: discounts above 50% are capped
        tnPercent = 50
    ENDIF
    lnResult = tnAmount - (tnAmount * tnPercent / 100)
    RETURN lnResult
ENDFUNC

PROCEDURE RevalueAll
    USE products
    SCAN FOR stock > 0
        REPLACE total_value WITH stock * unit_cost
    ENDSCAN
    DO ApplyDiscount
ENDPROC

PROCEDURE PurgeStale
    USE products
    SCAN FOR year < 2000
        UPDATE products SET stock = 0 WHERE year < 2000
    ENDSCAN
ENDPROC

PROCEDURE MonthlyReport
    LPARAMETERS tnYear
    SELECT product, SUM(total_value) FROM products ;
        WHERE year = tnYear ;
        GROUP BY product ;
        ORDER BY 2 DESC
ENDPROC
