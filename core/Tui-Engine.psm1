#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$TuiColors = @{
    Background=[ConsoleColor]::Black; Text=[ConsoleColor]::Gray; Highlight=[ConsoleColor]::White; HighlightBg=[ConsoleColor]::DarkCyan
    Title=[ConsoleColor]::Cyan; Accent=[ConsoleColor]::DarkCyan; Error=[ConsoleColor]::Red; Success=[ConsoleColor]::Green; Warning=[ConsoleColor]::Yellow
}

$TuiLayout = @{
    MarginX = 2
    HeaderY = 1
    ContentTop = 3
    ContentBottomPadding = 4
    LabelWidth = 24
    MinBoxWidth = 40
    MaxBoxWidth = 110
    FieldRowsMax = 12
    StatusRows = 2
}

function Get-TuiLayoutBounds {
    $width = [Math]::Max(1, [Console]::WindowWidth)
    $height = [Math]::Max(1, [Console]::WindowHeight)
    $contentHeight = [Math]::Max(4, $height - $TuiLayout.ContentTop - $TuiLayout.ContentBottomPadding)
    $boxWidth = [Math]::Max($TuiLayout.MinBoxWidth, [Math]::Min($TuiLayout.MaxBoxWidth, $width - ($TuiLayout.MarginX * 2)))
    $boxX = [Math]::Max(0, [Math]::Floor(($width - $boxWidth) / 2))
    return @{
        Width = $width
        Height = $height
        BoxX = $boxX
        BoxY = $TuiLayout.ContentTop
        BoxWidth = [Math]::Min($boxWidth, $width)
        ContentHeight = $contentHeight
        StatusRow = [Math]::Max(0, $height - 2)
        HelpRow = [Math]::Max(0, $height - 1)
    }
}

function Get-TuiClippedText {
    param([string]$Text,[int]$Width)
    $safeText = if ($null -eq $Text) { '' } else { [string]$Text }
    if ($Width -le 0) { return '' }
    if ($safeText.Length -le $Width) { return $safeText }
    if ($Width -le 3) { return '.' * $Width }
    return $safeText.Substring(0, $Width - 3) + '...'
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

function Show-TuiForm {
    param([Parameter(Mandatory)][hashtable[]]$Fields,[hashtable]$CurrentValues=@{},[string]$Title='Configure device')

    $result = @{}
    foreach ($f in $Fields) {
        $existing = if ($CurrentValues.ContainsKey($f.Name)) { [string]$CurrentValues[$f.Name] } else { '' }
        $result[$f.Name] = $existing
    }

    $index = 0
    $status = 'Fill out fields and choose Save.'
    while ($true) {
        $bounds = Get-TuiLayoutBounds
        $fieldCount = @($Fields).Count
        $actions = @('Save','Cancel')
        $totalSlots = $fieldCount + $actions.Count
        $index = [Math]::Max(0, [Math]::Min($index, $totalSlots - 1))

        $boxHeight = [Math]::Min($bounds.ContentHeight, [Math]::Max(10, [Math]::Min($TuiLayout.FieldRowsMax, $fieldCount) + 5))
        $visibleRows = [Math]::Max(1, $boxHeight - 4)
        $topIndex = if ($index -lt $fieldCount) {
            [Math]::Max(0, [Math]::Min($index - [Math]::Floor($visibleRows / 2), [Math]::Max(0, $fieldCount - $visibleRows)))
        } else {
            [Math]::Max(0, $fieldCount - $visibleRows)
        }

        Clear-TuiScreen
        Write-TuiAt -X $TuiLayout.MarginX -Y $TuiLayout.HeaderY -Text (Get-TuiClippedText -Text $Title -Width ($bounds.Width - ($TuiLayout.MarginX * 2))) -Fg $TuiColors.Title
        Write-TuiBox -X $bounds.BoxX -Y $bounds.BoxY -Width $bounds.BoxWidth -Height $boxHeight -Title ' Form '

        $labelX = $bounds.BoxX + 2
        $valueX = $labelX + $TuiLayout.LabelWidth + 1
        $valueWidth = [Math]::Max(4, $bounds.BoxWidth - ($valueX - $bounds.BoxX) - 2)

        for ($row=0; $row -lt $visibleRows; $row++) {
            $fieldIndex = $topIndex + $row
            if ($fieldIndex -ge $fieldCount) { break }
            $field = $Fields[$fieldIndex]
            $isSelected = $fieldIndex -eq $index
            $labelText = Get-TuiClippedText -Text ([string]$field.Label) -Width $TuiLayout.LabelWidth
            $valueRaw = if ($result.ContainsKey($field.Name)) { [string]$result[$field.Name] } else { '' }
            $valueDisplay = if ($field.Type -eq 'secret' -and -not [string]::IsNullOrEmpty($valueRaw)) { '*' * [Math]::Min(12, $valueRaw.Length) } else { $valueRaw }
            $valueText = Get-TuiClippedText -Text $valueDisplay -Width $valueWidth
            $fg = if ($isSelected) { $TuiColors.Highlight } else { $TuiColors.Text }
            $bg = if ($isSelected) { $TuiColors.HighlightBg } else { $TuiColors.Background }
            Write-TuiAt -X $labelX -Y ($bounds.BoxY + 1 + $row) -Text ($labelText.PadRight($TuiLayout.LabelWidth)) -Fg $fg -Bg $bg
            Write-TuiAt -X $valueX -Y ($bounds.BoxY + 1 + $row) -Text ($valueText.PadRight($valueWidth)) -Fg $fg -Bg $bg
        }

        $actionRow = $bounds.BoxY + $boxHeight - 2
        for ($a=0; $a -lt $actions.Count; $a++) {
            $slot = $fieldCount + $a
            $isSelected = $slot -eq $index
            $fg = if ($isSelected) { $TuiColors.Highlight } else { $TuiColors.Text }
            $bg = if ($isSelected) { $TuiColors.HighlightBg } else { $TuiColors.Background }
            $actionText = "[ $($actions[$a]) ]"
            Write-TuiAt -X ($bounds.BoxX + 4 + ($a * 14)) -Y $actionRow -Text $actionText -Fg $fg -Bg $bg
        }

        Show-TuiStatus -Message $status -Type Info -Row $bounds.StatusRow
        Show-TuiStatus -Message '↑/↓/Tab navigate  Enter edit/select  Esc cancel  Backspace delete' -Type Info -Row $bounds.HelpRow

        $k = Read-TuiKey
        switch ($k.Key) {
            'UpArrow' { $index = if ($index -le 0) { $totalSlots - 1 } else { $index - 1 } }
            'DownArrow' { $index = if ($index -ge ($totalSlots - 1)) { 0 } else { $index + 1 } }
            'Tab' {
                if (($k.Modifiers -band [ConsoleModifiers]::Shift) -eq [ConsoleModifiers]::Shift) {
                    $index = if ($index -le 0) { $totalSlots - 1 } else { $index - 1 }
                } else {
                    $index = if ($index -ge ($totalSlots - 1)) { 0 } else { $index + 1 }
                }
            }
            'Enter' {
                if ($index -ge $fieldCount) {
                    if ($actions[$index - $fieldCount] -eq 'Cancel') { return $null }
                    $missing = @($Fields | Where-Object { $_.Required -and [string]::IsNullOrWhiteSpace([string]$result[$_.Name]) })
                    if ($missing.Count -gt 0) {
                        $status = "Required: $($missing[0].Label)"
                        continue
                    }
                    return $result
                }

                $field = $Fields[$index]
                if ($field.Type -eq 'choice') {
                    $choices = @($field.Choices)
                    if ($choices.Count -eq 0) { $status = "No choices available for $($field.Label)."; continue }
                    $currentChoice = [string]$result[$field.Name]
                    $choiceIndex = [Array]::IndexOf([string[]]$choices, $currentChoice)
                    if ($choiceIndex -lt 0) { $choiceIndex = 0 } else { $choiceIndex = ($choiceIndex + 1) % $choices.Count }
                    $result[$field.Name] = [string]$choices[$choiceIndex]
                    $status = "$($field.Label): $($choices[$choiceIndex])"
                    continue
                }

                $status = "Editing $($field.Label). Type to update, Enter to finish."
                $editing = $true
                while ($editing) {
                    $ek = Read-TuiKey
                    switch ($ek.Key) {
                        'Enter' { $editing = $false }
                        'Escape' { $editing = $false }
                        'Backspace' {
                            $curr = [string]$result[$field.Name]
                            if ($curr.Length -gt 0) { $result[$field.Name] = $curr.Substring(0, $curr.Length - 1) }
                        }
                        default {
                            $ch = $ek.KeyChar
                            if (-not [char]::IsControl($ch)) {
                                $result[$field.Name] = ([string]$result[$field.Name]) + [string]$ch
                            }
                        }
                    }
                }
            }
            'Escape' { return $null }
            'Backspace' {
                if ($index -lt $fieldCount) {
                    $curr = [string]$result[$Fields[$index].Name]
                    if ($curr.Length -gt 0) { $result[$Fields[$index].Name] = $curr.Substring(0, $curr.Length - 1) }
                }
            }
        }
    }
}

function Show-TuiProgress { param([string]$Label,[int]$Row,[ValidateSet('Spinner','Done','Failed')][string]$State='Spinner'); Show-TuiStatus -Row $Row -Type Info -Message $Label }

Export-ModuleMember -Function @('Clear-TuiScreen','Write-TuiAt','Write-TuiBox','Read-TuiKey','Show-TuiStatus','Show-TuiMenu','Show-TuiForm','Show-TuiProgress','Get-TuiClippedText','Get-TuiLayoutBounds') -Variable @('TuiColors','TuiLayout')
