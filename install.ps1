$serviceName = "RTC Call Monitor"

if (Get-Service $serviceName -ErrorAction SilentlyContinue)
{
    remove-service $serviceName
    write-host "$($serviceName) service removed"
}
else
{
    write-host "$($serviceName) service does not exist"
}

write-host "installing service $($serviceName)"

$binaryPath = resolve-path .\RtcCallMonitor.exe
New-Service -name $serviceName -binaryPathName $binaryPath -displayName $serviceName -startupType Automatic


write-host "`n`n`ninstallation completed - Ensure that elevated credentials are entered in the Log On tab before starting the service"