#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$TuiColors = @{
    Background=[ConsoleColor]::Black; Text=[ConsoleColor]::Gray; Highlight=[ConsoleColor]::White; HighlightBg=[ConsoleColor]::DarkCyan
    Title=[ConsoleColor]::Cyan; Accent=[ConsoleColor]::DarkCyan; Error=[ConsoleColor]::Red; Success=[ConsoleColor]::Green; Warning=[ConsoleColor]::Yellow
}
$TuiLayout = @{
    MinWidth = 72
    MinHeight = 20
    MarginX = 2
    HeaderRows = 3
    FooterRows = 3
}

function Clear-TuiScreen { [Console]::BackgroundColor = $TuiColors.Background; [Console]::Clear(); [Console]::SetCursorPosition(0,0) }
function Read-TuiKey { [Console]::ReadKey($true) }
function Write-TuiAt {
    param(
        [int]$X,
        [int]$Y,
        [string]$Text,
        [ConsoleColor]$Fg=[ConsoleColor]::Gray,
        [ConsoleColor]$Bg=[ConsoleColor]::Black
    )

    $width = [Console]::WindowWidth
    $height = [Console]::WindowHeight
    if ($width -le 0 -or $height -le 0) { return }
    if ($X -lt 0 -or $Y -lt 0 -or $X -ge $width -or $Y -ge $height) { return }

    $output = if ($null -eq $Text) { '' } else { [string]$Text }
    $available = $width - $X
    if ($available -le 0) { return }
    if ($output.Length -gt $available) { $output = $output.Substring(0, $available) }

    [Console]::ForegroundColor = $Fg
    [Console]::BackgroundColor = $Bg
    [Console]::SetCursorPosition($X, $Y)
    [Console]::Write($output)
    [Console]::ForegroundColor = $TuiColors.Text
    [Console]::BackgroundColor = $TuiColors.Background
}
function Write-TuiBox { param([int]$X,[int]$Y,[int]$Width,[int]$Height,[string]$Title=''); if($Width -lt 2 -or $Height -lt 2){return}; Write-TuiAt $X $Y ('+'+('-'*($Width-2))+'+') $TuiColors.Accent; for($i=1;$i -lt ($Height-1);$i++){ Write-TuiAt $X ($Y+$i) ('|' + (' '*($Width-2)) + '|') $TuiColors.Accent }; Write-TuiAt $X ($Y+$Height-1) ('+'+('-'*($Width-2))+'+') $TuiColors.Accent; if($Title){ Write-TuiAt ($X+2) $Y $Title $TuiColors.Title } }
function Show-TuiStatus {
    param(
        [string]$Message,
        [ValidateSet('Info','Success','Error','Warning')][string]$Type='Info',
        [int]$Row
    )

    $fg = switch ($Type) {
        'Success' { $TuiColors.Success }
        'Error' { $TuiColors.Error }
        'Warning' { $TuiColors.Warning }
        default { $TuiColors.Text }
    }

    $height = [Console]::WindowHeight
    if ($height -le 0) { return }
    $safeRow = [Math]::Max(0, [Math]::Min($Row, $height - 1))
    $width = [Math]::Max(1, [Console]::WindowWidth)
    $safeMessage = if ($null -eq $Message) { '' } else { [string]$Message }
    Write-TuiAt 0 $safeRow ($safeMessage.PadRight($width)) $fg
}

function Show-TuiMenu {
    param(
        [Parameter(Mandatory)][hashtable]$Menu,
        [int]$X=2,
        [int]$Y=4,
        [switch]$DisableSubmenuRecursion
    )
    $selected = 0
    while ($true) {
        $bounds = Get-TuiLayoutBounds
        $items = @($Menu.Items)
        $boxHeight = [Math]::Min($bounds.ContentHeight, [Math]::Max(8, $items.Count + 4))
        Clear-TuiScreen
        Write-TuiAt -X $TuiLayout.MarginX -Y $TuiLayout.HeaderY -Text (Get-TuiClippedText -Text $Menu.Title -Width ($bounds.Width - ($TuiLayout.MarginX * 2))) -Fg $TuiColors.Title
        Write-TuiBox -X $bounds.BoxX -Y $bounds.BoxY -Width $bounds.BoxWidth -Height $boxHeight -Title ' Menu '

        $visibleRows = [Math]::Max(1, $boxHeight - 2)
        $topIndex = [Math]::Max(0, [Math]::Min($selected - [Math]::Floor($visibleRows / 2), [Math]::Max(0, $items.Count - $visibleRows)))
        for ($row=0; $row -lt $visibleRows; $row++) {
            $i = $topIndex + $row
            if ($i -ge $items.Count) { break }
            $item = $items[$i]
            $isSelected = $i -eq $selected
            $bg = if ($isSelected) { $TuiColors.HighlightBg } else { $TuiColors.Background }
            $fg = if ($isSelected) { $TuiColors.Highlight } else { $TuiColors.Text }
            $prefix = if ($item.Type -eq 'submenu') { '> ' } else { '  ' }
            $line = Get-TuiClippedText -Text ($prefix + $item.Label) -Width ($bounds.BoxWidth - 4)
            Write-TuiAt -X ($bounds.BoxX + 2) -Y ($bounds.BoxY + 1 + $row) -Text $line.PadRight($bounds.BoxWidth - 4) -Fg $fg -Bg $bg
        }

        Show-TuiStatus -Message '↑/↓ move  Enter select  Esc back' -Type Info -Row $bounds.HelpRow
        $k = Read-TuiKey
        switch ($k.Key) {
            'UpArrow' { $selected = if ($selected -le 0) { $items.Count-1 } else { $selected-1 } }
            'DownArrow' { $selected = if ($selected -ge $items.Count-1) { 0 } else { $selected+1 } }
            'Enter' {
                $item = $items[$selected]
                if ($item.Type -eq 'back') { return $null }
                if ($item.Type -eq 'submenu') {
                    if ($DisableSubmenuRecursion) { return $item.Key }
                    $child = Show-TuiMenu -Menu @{ Title=$item.Label; Items=($item.Items + @(@{Key='back';Label='.. Back';Type='back'})) }
                    if ($child) { return $child }
                    continue
                }
                return $item.Key
            }
            'Escape' { return $null }
            'Backspace' { return $null }
        }
    }
}

function Get-TuiFormLayout {
    param(
        [int]$FieldCount
    )

    $windowWidth = [Console]::WindowWidth
    $windowHeight = [Console]::WindowHeight
    $minWidth = [Math]::Max(40, $TuiLayout.MinWidth)
    $minHeight = [Math]::Max(12, $TuiLayout.MinHeight)
    if ($windowWidth -lt $minWidth -or $windowHeight -lt $minHeight) {
        throw "Console window is too small for form rendering. Current: ${windowWidth}x${windowHeight}. Required minimum: ${minWidth}x${minHeight}."
    }

    $margin = [Math]::Max(1, $TuiLayout.MarginX)
    $width = [Math]::Max(40, [Math]::Min(100, $windowWidth - ($margin * 2)))
    $height = [Math]::Max(12, [Math]::Min($windowHeight - 2, $FieldCount + $TuiLayout.HeaderRows + $TuiLayout.FooterRows + 5))
    $x = [Math]::Max(0, [Math]::Floor(($windowWidth - $width) / 2))
    $y = [Math]::Max(0, [Math]::Floor(($windowHeight - $height) / 2))
    return @{
        X = $x
        Y = $y
        Width = $width
        Height = $height
        ContentStartRow = $y + $TuiLayout.HeaderRows
        HelpRow = $y + $height - 3
        StatusRow = $y + $height - 2
    }
}

function Read-TuiLineInput {
    param(
        [string]$InitialValue = '',
        [int]$MaxLength = 1024,
        [bool]$MaskInput = $false
    )

    $buffer = if ($null -eq $InitialValue) { '' } else { [string]$InitialValue }
    while ($true) {
        $key = Read-TuiKey
        switch ($key.Key) {
            'Enter' { return @{ Accepted = $true; Value = $buffer } }
            'Escape' { return @{ Accepted = $false; Value = $InitialValue } }
            'Backspace' {
                if ($buffer.Length -gt 0) {
                    $buffer = $buffer.Substring(0, $buffer.Length - 1)
                }
            }
            default {
                if (-not [char]::IsControl($key.KeyChar) -and $buffer.Length -lt $MaxLength) {
                    $buffer += [string]$key.KeyChar
                }
            }
        }
        $display = if ($MaskInput) { ''.PadLeft($buffer.Length, '*') } else { $buffer }
        $padWidth = [Math]::Max(1, [Math]::Min([Math]::Max(1, [Console]::WindowWidth - [Console]::CursorLeft - 1), $MaxLength))
        [Console]::Write(("`r{0}" -f $display.PadRight($padWidth)))
    }
}

function Show-TuiForm {
    param([Parameter(Mandatory)][hashtable[]]$Fields,[hashtable]$CurrentValues=@{},[string]$Title='Configure device')

    $values = @{}
    foreach ($f in $Fields) {
        $defaultValue = if ($CurrentValues.ContainsKey($f.Name)) { $CurrentValues[$f.Name] } elseif ($f.ContainsKey('Placeholder')) { $f.Placeholder } else { '' }
        $values[$f.Name] = [string]$defaultValue
    }

    $selected = 0
    while ($true) {
        $layout = Get-TuiFormLayout -FieldCount $Fields.Count
        Clear-TuiScreen
        Write-TuiBox -X $layout.X -Y $layout.Y -Width $layout.Width -Height $layout.Height -Title $Title
        Write-TuiAt -X ($layout.X + 2) -Y ($layout.Y + 1) -Text 'Up/Down navigate  Enter edit/select  F10 save  Esc cancel' -Fg $TuiColors.Accent

        for ($i = 0; $i -lt $Fields.Count; $i++) {
            $field = $Fields[$i]
            $isSelected = $i -eq $selected
            $labelText = ("{0}{1}" -f $field.Label, $(if ($field.Required) { ' *' } else { '' }))
            $fieldValue = [string]$values[$field.Name]
            if ($field.Type -eq 'secret' -and -not [string]::IsNullOrEmpty($fieldValue)) {
                $fieldValue = ''.PadLeft($fieldValue.Length, '*')
            }
            if ([string]::IsNullOrWhiteSpace($fieldValue) -and $field.ContainsKey('Placeholder')) {
                $fieldValue = "<$($field.Placeholder)>"
            }

            $rowY = $layout.ContentStartRow + $i
            $fg = if ($isSelected) { $TuiColors.Highlight } else { $TuiColors.Text }
            $bg = if ($isSelected) { $TuiColors.HighlightBg } else { $TuiColors.Background }
            Write-TuiAt -X ($layout.X + 2) -Y $rowY -Text ($labelText.PadRight(32)) -Fg $fg -Bg $bg
            Write-TuiAt -X ($layout.X + 35) -Y $rowY -Text ($fieldValue.PadRight([Math]::Max(1, $layout.Width - 38))) -Fg $fg -Bg $bg
        }

        $helpText = if ($Fields[$selected].ContainsKey('HelpText')) { [string]$Fields[$selected].HelpText } else { '' }
        Show-TuiStatus -Message $helpText -Type Info -Row $layout.HelpRow
        Show-TuiStatus -Message '' -Type Info -Row $layout.StatusRow

        $key = Read-TuiKey
        switch ($key.Key) {
            'UpArrow' { $selected = if ($selected -le 0) { $Fields.Count - 1 } else { $selected - 1 } }
            'DownArrow' { $selected = if ($selected -ge $Fields.Count - 1) { 0 } else { $selected + 1 } }
            'Escape' { return $null }
            'F10' {
                $missing = @()
                foreach ($field in $Fields) {
                    if ($field.Required -and [string]::IsNullOrWhiteSpace([string]$values[$field.Name])) {
                        $missing += [string]$field.Label
                    }
                }
                if ($missing.Count -gt 0) {
                    Show-TuiStatus -Message ("Required fields missing: {0}" -f ($missing -join ', ')) -Type Error -Row $layout.StatusRow
                    Start-Sleep -Milliseconds 1400
                    continue
                }
                return $values
            }
            'Enter' {
                $activeField = $Fields[$selected]
                if ($activeField.Type -eq 'choice') {
                    $choices = @($activeField.Choices)
                    if ($choices.Count -eq 0) {
                        Show-TuiStatus -Message "No choices configured for $($activeField.Label)." -Type Error -Row $layout.StatusRow
                        Start-Sleep -Milliseconds 1000
                        continue
                    }
                    $currentIndex = [Array]::IndexOf($choices, [string]$values[$activeField.Name])
                    if ($currentIndex -lt 0) { $currentIndex = 0 }
                    $values[$activeField.Name] = [string]$choices[(($currentIndex + 1) % $choices.Count)]
                    continue
                }

                $promptX = $layout.X + 2
                $promptY = $layout.StatusRow
                $currentValue = [string]$values[$activeField.Name]
                $promptLabel = "Edit $($activeField.Label): "
                $mask = $activeField.Type -eq 'secret'
                $maxInput = [Math]::Max(8, $layout.Width - $promptLabel.Length - 6)
                Write-TuiAt -X $promptX -Y $promptY -Text ($promptLabel.PadRight($layout.Width - 4)) -Fg $TuiColors.Accent
                [Console]::SetCursorPosition($promptX + $promptLabel.Length, $promptY)
                $lineEdit = Read-TuiLineInput -InitialValue $currentValue -MaskInput:$mask -MaxLength $maxInput
                if ($lineEdit.Accepted) {
                    $values[$activeField.Name] = [string]$lineEdit.Value
                }
            }
        }
    }
}

function Show-TuiProgress { param([string]$Label,[int]$Row,[ValidateSet('Spinner','Done','Failed')][string]$State='Spinner'); Show-TuiStatus -Row $Row -Type Info -Message $Label }

Export-ModuleMember `
    -Function @(
        'Clear-TuiScreen',
        'Write-TuiAt',
        'Write-TuiBox',
        'Read-TuiKey',
        'Show-TuiStatus',
        'Show-TuiMenu',
        'Show-TuiForm',
        'Show-TuiProgress'
    ) `
    -Variable @(
        'TuiColors',
        'TuiLayout'
    )
