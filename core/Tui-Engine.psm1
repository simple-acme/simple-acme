#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$TuiColors = @{
    Background=[ConsoleColor]::Black; Text=[ConsoleColor]::Gray; Highlight=[ConsoleColor]::White; HighlightBg=[ConsoleColor]::DarkCyan
    Title=[ConsoleColor]::Cyan; Accent=[ConsoleColor]::DarkCyan; Error=[ConsoleColor]::Red; Success=[ConsoleColor]::Green; Warning=[ConsoleColor]::Yellow
}

function Clear-TuiScreen { [Console]::BackgroundColor = $TuiColors.Background; [Console]::Clear(); [Console]::SetCursorPosition(0,0) }
function Read-TuiKey { [Console]::ReadKey($true) }
function Write-TuiAt { param([int]$X,[int]$Y,[string]$Text,[ConsoleColor]$Fg=[ConsoleColor]::Gray,[ConsoleColor]$Bg=[ConsoleColor]::Black); [Console]::ForegroundColor=$Fg; [Console]::BackgroundColor=$Bg; [Console]::SetCursorPosition($X,$Y); [Console]::Write($Text); [Console]::ForegroundColor=$TuiColors.Text; [Console]::BackgroundColor=$TuiColors.Background }
function Write-TuiBox { param([int]$X,[int]$Y,[int]$Width,[int]$Height,[string]$Title=''); if($Width -lt 2 -or $Height -lt 2){return}; Write-TuiAt $X $Y ('+'+('-'*($Width-2))+'+') $TuiColors.Accent; for($i=1;$i -lt ($Height-1);$i++){ Write-TuiAt $X ($Y+$i) ('|' + (' '*($Width-2)) + '|') $TuiColors.Accent }; Write-TuiAt $X ($Y+$Height-1) ('+'+('-'*($Width-2))+'+') $TuiColors.Accent; if($Title){ Write-TuiAt ($X+2) $Y $Title $TuiColors.Title } }
function Show-TuiStatus { param([string]$Message,[ValidateSet('Info','Success','Error','Warning')][string]$Type='Info',[int]$Row); $fg=switch($Type){'Success'{$TuiColors.Success}'Error'{$TuiColors.Error}'Warning'{$TuiColors.Warning}default{$TuiColors.Text}}; Write-TuiAt 0 $Row ($Message.PadRight([Console]::WindowWidth)) $fg }

function Show-TuiMenu {
    param([Parameter(Mandatory)][hashtable]$Menu,[int]$X=2,[int]$Y=4)
    $selected = 0
    while ($true) {
        Clear-TuiScreen
        Write-TuiBox -X $X -Y ($Y-2) -Width ([Math]::Min(90,[Console]::WindowWidth-4)) -Height ([Math]::Max(8,$Menu.Items.Count+4)) -Title $Menu.Title
        for ($i=0; $i -lt $Menu.Items.Count; $i++) {
            $item = $Menu.Items[$i]
            $bg = if ($i -eq $selected) { $TuiColors.HighlightBg } else { $TuiColors.Background }
            $fg = if ($i -eq $selected) { $TuiColors.Highlight } else { $TuiColors.Text }
            Write-TuiAt -X ($X+2) -Y ($Y+$i) -Text ($item.Label.PadRight(45)) -Fg $fg -Bg $bg
        }
        $k = Read-TuiKey
        switch ($k.Key) {
            'UpArrow' { $selected = if ($selected -le 0) { $Menu.Items.Count-1 } else { $selected-1 } }
            'DownArrow' { $selected = if ($selected -ge $Menu.Items.Count-1) { 0 } else { $selected+1 } }
            'Enter' {
                $item = $Menu.Items[$selected]
                if ($item.Type -eq 'back') { return $null }
                if ($item.Type -eq 'submenu') {
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
        $label = $f.Label
        $existing = if ($CurrentValues.ContainsKey($f.Name)) { [string]$CurrentValues[$f.Name] } else { '' }
        if ($f.Type -eq 'choice') {
            $choices = @($f.Choices)
            for($i=0;$i -lt $choices.Count;$i++){ [Console]::WriteLine(("[{0}] {1}" -f ($i+1),$choices[$i])) }
            $selection = Read-Host "$label (number)"
            $index = 0
            if(-not [int]::TryParse($selection,[ref]$index) -or $index -lt 1 -or $index -gt $choices.Count){ throw "Invalid choice for $label" }
            $result[$f.Name] = [string]$choices[$index-1]
            continue
        }
        if ($f.Type -eq 'secret') {
            $secure = Read-Host -Prompt $label -AsSecureString
            $input = [System.Net.NetworkCredential]::new('',$secure).Password
        } else {
            $prompt = if ($existing) { "$label [$existing]" } else { $label }
            $input = Read-Host -Prompt $prompt
            if ([string]::IsNullOrEmpty($input)) { $input = $existing }
        }
        if ($f.Required -and [string]::IsNullOrWhiteSpace($input)) { throw "Field '$($f.Name)' is required." }
        $result[$f.Name] = $input
    }
    $result
}

function Show-TuiProgress { param([string]$Label,[int]$Row,[ValidateSet('Spinner','Done','Failed')][string]$State='Spinner'); Show-TuiStatus -Row $Row -Type Info -Message $Label }

Export-ModuleMember -Function @('Clear-TuiScreen','Write-TuiAt','Write-TuiBox','Read-TuiKey','Show-TuiStatus','Show-TuiMenu','Show-TuiForm','Show-TuiProgress') -Variable TuiColors
