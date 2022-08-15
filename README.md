# RTC-Call-Monitor

## About

RTC-Call-Monitor is a tool to detect an active UDP based voice/video call by:

- Monitoring the rate of incoming/outgoing UDP packets from your machine's IP address.
- Checking the source/destination address against known network blocks for the major voice/video conference providers.

When an active call is detected, will invoke a webhook for the start and end of a call.

### Supported Providers

- [Zoom](https://assets.zoom.us/docs/ipranges/ZoomMeetings.txt)
- Microsoft Teams
- WebEx
- Slack
- Google Meet
- GoTo Meeting

Additional providers can be added by modifying the configuration file. See _Adding new providers_ below.

Consolidating subnet ranges (optional): `python .\consolidate-ips.py --input "D:\Downloads\ZoomMeetings.txt"`

## Getting Started

### Prerequisites

- [.NET 5.0 SDK](https://dotnet.microsoft.com/download/dotnet/5.0)

### Minimal Configuration

Before starting the application for the first time, modify the value for `LocalNetwork` in _appsettings.json_ with the details of your local network. The value is in [CIDR Notation](https://www.digitalocean.com/community/tutorials/understanding-ip-addresses-subnets-and-cidr-notation-for-networking).

Most users will have a network configuration similar to the following:

```bash
  IP Address:  192.168.1.100
  Subnet Mask: 255.255.255.0
```

The configuration will be

`"LocalNetwork": "192.168.1.0/24"`

### Build/Run

RTC-Call-Monitor requires that your console has elevated privileges in order to monitor network traffic.

Windows: Open a new Powershell or command prompt using _Run as Administrator_

```bash
cd src
dotnet run
```

Linux/macOS:

```bash
cd src
sudo dotnet run
```

### Additional Configuration

#### Webhooks

All webhooks are HTTP POST requests and require a valid URL.

##### Call Start

`CallStart` in _appsettings.json_

POST body included when calling the webhook

```json
{
  "provider": "slack"
}
```

##### Call End

`CallEnd` in _appsettings.json_

POST body included when calling the webhook

```json
{
  "duration": 123
}
```

`duration` is the call length in seconds.

#### Adding new providers

The configuration key `KnownNetworks` is a JSON dictionary with the key being the provider name, and value is an array of CIDR network blocks. To add a new provider, add a new key/value pair to `KnownNetworks`.

#### Adding new network blocks

If the application is not detecting the start of your call, the most likely reason is that the IP address is not in a known network block for your provider. To see the destination address of your call provider, enable debug logging.

In _appsettings.json:_
`"RtcCallMonitor": "Debug"`

In the console a message similar to the following will show in the console:

`Unmapped network 66.77.89.91 count 122`

Find your provider in _appsettings.json_ and add a new entry for the network block

`66.77.0.0/16`

Restart the application and your call should be detected.
