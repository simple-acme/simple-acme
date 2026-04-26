@{
    Severity = @('Error', 'Warning', 'ParseError')
    IncludeRules = @(
        'PSAvoidUsingInvokeExpression',
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSUseApprovedVerbs',
        'PSUseDeclaredVarsMoreThanAssignments'
    )
    Rules = @{
        PSAvoidUsingPlainTextForPassword = @{
            Enable = $true
        }
        PSAvoidUsingConvertToSecureStringWithPlainText = @{
            Enable = $true
        }
    }
}
