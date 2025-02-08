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
    $result = & nfm.exe filesystem --rootdirectory "$((Get-Location).Path)"
      if($result) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($result)
      }
  } -ViMode $viMode

  Set-PSReadLineKeyHandler -Key "ctrl+r" -ScriptBlock {
    $result = $null
      $line = $null
      $cursor = $null
      [Microsoft.PowerShell.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)
      $result = nfm.exe filereader --path "$((Get-PSReadLineOption).HistorySavePath)" --searchstring "$($line)"
        if ($result) {
          [Microsoft.PowerShell.PSConsoleReadLine]::RevertLine()
          [Microsoft.PowerShell.PSConsoleReadLine]::Insert($result)
        }
  } -ViMode $viMode
}

function tab_expansion_preview {
  param($directory, $item)
  $trimmed = $item -replace '^\S+\s*', ''
  if($item.StartsWith("ProviderItem")) { 
    cmd /c type "$($directory)\$($trimmed)" 
  } elseif($item.StartsWith("ProviderContainer")) { 
    cmd /c dir "$($directory)\$($trimmed)" 
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
