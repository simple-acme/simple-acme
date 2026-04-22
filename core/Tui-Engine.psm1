Set-StrictMode -Version Latest

$script:TuiColors = @{
    Background  = [ConsoleColor]::Black
    Text        = [ConsoleColor]::Gray
    Highlight   = [ConsoleColor]::White
    HighlightBg = [ConsoleColor]::DarkCyan
    Title       = [ConsoleColor]::Cyan
    Accent      = [ConsoleColor]::DarkCyan
    Error       = [ConsoleColor]::Red
    Success     = [ConsoleColor]::Green
    Warning     = [ConsoleColor]::Yellow
    Muted       = [ConsoleColor]::DarkGray
    SecretMask  = [ConsoleColor]::DarkGray
}

function Clear-TuiScreen { [Console]::BackgroundColor = $script:TuiColors.Background; [Console]::Clear(); [Console]::SetCursorPosition(0,0) }
function Read-TuiKey { [Console]::ReadKey($true) }

function Write-TuiAt {
    param([int]$X,[int]$Y,[string]$Text,[ConsoleColor]$Fg=[ConsoleColor]::Gray,[ConsoleColor]$Bg=[ConsoleColor]::Black)
    $left = [Console]::CursorLeft; $top = [Console]::CursorTop
    try {
        [Console]::ForegroundColor = $Fg; [Console]::BackgroundColor = $Bg
        [Console]::SetCursorPosition($X,$Y); [Console]::Write($Text)
    } finally {
        [Console]::ForegroundColor = $script:TuiColors.Text; [Console]::BackgroundColor = $script:TuiColors.Background
        [Console]::SetCursorPosition($left,$top)
    }
}

function Write-TuiBox {
    param([int]$X,[int]$Y,[int]$Width,[int]$Height,[string]$Title='',[ConsoleColor]$BorderColor=[ConsoleColor]::DarkCyan)
    if ($Width -lt 2 -or $Height -lt 2) { return }
    Write-TuiAt -X $X -Y $Y -Text ('┌' + ('─' * ($Width-2)) + '┐') -Fg $BorderColor
    for ($i=1; $i -lt ($Height-1); $i++) { Write-TuiAt -X $X -Y ($Y+$i) -Text ('│' + (' ' * ($Width-2)) + '│') -Fg $BorderColor }
    Write-TuiAt -X $X -Y ($Y+$Height-1) -Text ('└' + ('─' * ($Width-2)) + '┘') -Fg $BorderColor
    if ($Title) {
        $t = " $Title "; $tx = $X + [Math]::Max(1,[int](($Width-$t.Length)/2)); Write-TuiAt -X $tx -Y $Y -Text $t -Fg $script:TuiColors.Title
    }
}

function Show-TuiStatus {
    param([string]$Message,[ValidateSet('Info','Success','Error','Warning')][string]$Type='Info',[int]$Row)
    $fg = switch ($Type) { 'Success' {$script:TuiColors.Success} 'Error' {$script:TuiColors.Error} 'Warning' {$script:TuiColors.Warning} default {$script:TuiColors.Text} }
    $msg = $Message.PadRight([Console]::WindowWidth)
    Write-TuiAt -X 0 -Y $Row -Text $msg -Fg $fg -Bg $script:TuiColors.Background
}

function Show-TuiMenu {
    param([Parameter(Mandatory)][hashtable]$Menu,[int]$X=2,[int]$Y=4)
    $selected = 0
    while ($true) {
        Clear-TuiScreen
        $height = [Math]::Max(8, $Menu.Items.Count + 4)
        Write-TuiBox -X $X -Y ($Y-2) -Width ([Math]::Min(90,[Console]::WindowWidth-4)) -Height $height -Title $Menu.Title
        for ($i=0; $i -lt $Menu.Items.Count; $i++) {
            $item = $Menu.Items[$i]
            $bg = if ($i -eq $selected) { $script:TuiColors.HighlightBg } else { $script:TuiColors.Background }
            $fg = if ($i -eq $selected) { $script:TuiColors.Highlight } else { $script:TuiColors.Text }
            Write-TuiAt -X ($X+2) -Y ($Y+$i) -Text ($item.Label.PadRight(45)) -Fg $fg -Bg $bg
        }
        $k = Read-TuiKey
        switch ($k.Key) {
            'UpArrow' { $selected = if ($selected -le 0) { $Menu.Items.Count-1 } else { $selected-1 } }
            'DownArrow' { $selected = if ($selected -ge $Menu.Items.Count-1) { 0 } else { $selected+1 } }
            'Enter' { return [string]$Menu.Items[$selected].Key }
            'Escape' { return $null }
            'Backspace' { return $null }
            default {
                if ($k.KeyChar -eq 'q' -or $k.KeyChar -eq 'Q') { return $null }
            }
        }
    }
}

function Show-TuiForm {
    param([Parameter(Mandatory)][hashtable[]]$Fields,[hashtable]$CurrentValues=@{},[string]$Title='Configure device')
    $result = @{}
    foreach ($f in $Fields) {
        $label = $f.Label
        $existing = if ($CurrentValues.ContainsKey($f.Name)) { [string]$CurrentValues[$f.Name] } else { '' }
        if ($f.Type -eq 'choice') {
            $choiceKey = if ($f.ContainsKey('ChoiceFieldName') -and -not [string]::IsNullOrWhiteSpace([string]$f.ChoiceFieldName)) { [string]$f.ChoiceFieldName } else { [string]$f.Name }
            $choices = @()
            if ($f.ContainsKey('Choices') -and $null -ne $f.Choices) { $choices = @($f.Choices) }
            $value = if ($existing) { $existing } elseif ($choices.Count -gt 0) { [string]$choices[0] } else { '' }
            if ($f.Required -and [string]::IsNullOrWhiteSpace($value)) { throw "Field '$choiceKey' is required." }
            $result[$choiceKey] = $value
            continue
        }
        if ($existing) { $prompt = "$label [$existing]" } else { $prompt = $label }
        if ($f.Type -eq 'secret') {
            $input = Read-Host -Prompt "$prompt (input hidden in TUI mode; plain console prompt fallback)"
        } else {
            $input = Read-Host -Prompt $prompt
        }
        if ([string]::IsNullOrEmpty($input)) { $input = $existing }
        if ($f.Required -and [string]::IsNullOrWhiteSpace($input)) { throw "Field '$($f.Name)' is required." }
        $result[$f.Name] = $input
    }
    return $result
}

function Show-TuiProgress {
    param([string]$Label,[int]$Row,[ValidateSet('Spinner','Done','Failed')][string]$State='Spinner')
    $glyph = switch ($State) { 'Done' {'✔'} 'Failed' {'✖'} default {'⠋'} }
    $fg = switch ($State) { 'Done' {$script:TuiColors.Success} 'Failed' {$script:TuiColors.Error} default {$script:TuiColors.Accent} }
    Show-TuiStatus -Row $Row -Type Info -Message "$glyph $Label"
}

Export-ModuleMember -Function @('Clear-TuiScreen','Write-TuiAt','Write-TuiBox','Read-TuiKey','Show-TuiStatus','Show-TuiMenu','Show-TuiForm','Show-TuiProgress') -Variable TuiColors
