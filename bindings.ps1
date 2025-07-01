foreach ($viMode in @('Command', 'Insert')) {
  Set-PSReadLineKeyHandler -Key "alt+o" -ScriptBlock {
    $result = & nfm.exe filesystem --directoriesonly --maxdepth 10 --rootdirectory "$($env:USERPROFILE)"
      if($result) {
        Set-Location $result
          [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine()
      }
  } -ViMode $viMode

  Set-PSReadLineKeyHandler -Key "ctrl+o" -ScriptBlock {
    $result = & nfm.exe filesystem --maxdepth 10 --rootdirectory "$((Get-Location).Path)" --directoriesonly
      if($result) {
        Set-Location $result
          [Microsoft.PowerShell.PSConsoleReadLine]::RevertLine()
          [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine()
      }
  } -ViMode $viMode

  Set-PSReadLineKeyHandler -Key "alt+t" -ScriptBlock {
    $result = & nfm.exe filesystem --searchdirectoryonselect --rootdirectory "$($env:USERPROFILE)"
      if($result) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($result)
      }
  } -ViMode $viMode

  Set-PSReadLineKeyHandler -Key "ctrl+t" -ScriptBlock {
    $result = & nfm.exe filesystem --haspreview --rootdirectory "$((Get-Location).Path)"
      if($result) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($result)
      }
  } -ViMode $viMode

  Set-PSReadLineKeyHandler -Key "ctrl+r" -ScriptBlock {
    $result = $null
      $line = $null
      $cursor = $null
      [Microsoft.PowerShell.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)
      $result = PowershellHistoryReader.exe | bat --color=always --style=plain --theme="Visual Studio Dark+" --language=ps1 | nfm.exe --linecontinuation `` --gap --wrap --searchstring "$($line)" --nolengthsort
        if ($result) {
          [Microsoft.PowerShell.PSConsoleReadLine]::RevertLine()
          $result | % {
            if($_.EndsWith('`')) {
              [Microsoft.PowerShell.PSConsoleReadLine]::Insert("$($_.Trim('`'))`n")
            } else {
              [Microsoft.PowerShell.PSConsoleReadLine]::Insert($_.Trim('`'))
            }
          }
        }
  } -ViMode $viMode
}

function gs {
  param($searchString)

  if ([string]::IsNullOrWhiteSpace($searchString)) {
    $searchString = Read-Host "Search String"
  }

  $selection = rg --crlf --no-messages --hidden -i --vimgrep $searchString | fzf --delimiter=: --preview "bat --color=always --style=numbers --theme=gruvbox-dark --highlight-line {2} {1}" --preview-window="+{2}+3/2" --preview-window=up

  if (-not $selection) {
    exit
  }

  # Extract file path and line number
  $parts = $selection -split ":", 3
  if ($parts.Count -lt 2) {
    exit
  }

  $dest = $parts[0]
  $line = $parts[1]

  if (-not $dest -or -not $line) {
    exit
  }

  # Open the file in Neovim at the specified line
  nvim "$dest" +$line
}

function tab_expansion_preview {
  param($directory, $item)
  $trimmed = $item -replace '^\S+\s*', ''
  if($item.StartsWith("ProviderItem")) { 
    bat -H 5 --paging=never --color=always --style=numbers --theme=gruvbox-dark "$($directory)\$($trimmed)" 
  } elseif($item.StartsWith("ProviderContainer")) { 
    dir "$($directory)\$($trimmed)" 
  } else { 
    Get-Help $trimmed 
  }
}

Set-PSReadLineKeyHandler -Key "ctrl+spacebar" -ScriptBlock {
  [void] [System.Reflection.Assembly]::LoadWithPartialName("System.Management.Automation")
  $line = $null
  $cursor = $null
  [Microsoft.PowerShell.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)

  if ($cursor -lt 0 -or [string]::IsNullOrWhiteSpace($line)) {
      return $false
  }

  try {
      $completions = [System.Management.Automation.CommandCompletion]::CompleteInput($line, $cursor, @{})
  }
  catch {
      # some custom tab completions will cause CompleteInput() to throw, so we gracefully handle those cases.
      # For example, see the issue https://github.com/kelleyma49/PSFzf/issues/95.
      return $false
  }
  $pwshCommand = 'pwsh -nologo -C tab_expansion_preview ' + '"' + (Get-Location).Path + '"' + " '{0}'"

  $result = ($completions.CompletionMatches | % { "{0,-30}{1}" -f $_.ResultType, $_.CompletionText } | nfm --previewcommand $pwshCommand --header ("{0,-30}{1}" -f "Type","Command"))
  $trimmed = $result -replace '^\S+\s*', ''

  if($result) {
    $leftCursor = $completions.ReplacementIndex
    $replacementLength = $completions.ReplacementLength
    if ($leftCursor -le 0 -and $replacementLength -le 0) {
      [Microsoft.PowerShell.PSConsoleReadLine]::Insert($trimmed)
    }
    else {
      [Microsoft.PowerShell.PSConsoleReadLine]::Replace($leftCursor, $replacementLength, $trimmed)
    }
  }
}


function gs {
  param($searchString)

  if ([string]::IsNullOrWhiteSpace($searchString)) {
    $searchString = Read-Host "Search String"
  }

  $selection = rg --crlf --no-messages --hidden -i --vimgrep $searchString | nfm.exe --previewcommand 'bat --color=always --style=numbers --theme=gruvbox-dark --highlight-line "{1}" "{0}"' --delimiter : --previewstartlinecommand "{1}" --previewstartlineoffsetcommand 3

  if (-not $selection) {
    return
  }

  $parts = $selection -split ":", 3
  if ($parts.Count -lt 2) {
    return
  }

  $dest = $parts[0]
  $line = $parts[1]

  if (-not $dest -or -not $line) {
    return
  }

  nvim "$dest" +$line
}
