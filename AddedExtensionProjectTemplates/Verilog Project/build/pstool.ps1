$job  = start-job {ping 192.168.1.1}
Get-wmiobject -list *thread*
Start-ThreadJob -ScriptBlock { Get-Process }