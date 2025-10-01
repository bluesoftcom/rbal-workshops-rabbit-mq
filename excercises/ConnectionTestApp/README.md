# BasicPublisherApp - RabbitMQ Workshop

A .NET 9.0 console application that demonstrates how to publish messages to RabbitMQ using the RabbitMQ.Client library.

## Prerequisites

Before running this application, ensure you have:

- ✅ **Visual Studio Code** (or another IDE like Visual Studio)
- ✅ **.NET 9.0 SDK** installed
- 🔧 **RabbitMQ Server** access (local installation or cloud instance)

## Quick Start

### 1. Clone and Navigate to Project

```powershell
# Navigate to the BasicPublisherApp directory
cd excercises\BasicPublisherApp
```

### 2. Configure RabbitMQ Connection

Edit the `App.config` file in the parent `excercises` folder with your RabbitMQ connection details:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="host" value="b-15808a31-fbf8-472a-81b9-d0e2f12d13f8.mq.eu-north-1.on.aws" />
    <add key="port" value="5671" />
    <add key="userName" value="your_username" />
    <add key="password" value="your_password" />
    <add key="virtualHost" value="/your_virtual_host" />
  </appSettings>
</configuration>
```

**Configuration Parameters:**
- **host**: Your RabbitMQ server hostname (b-15808a31-fbf8-472a-81b9-d0e2f12d13f8.mq.eu-north-1.on.aws)
- **port**: RabbitMQ port (5671)
- **userName**: Your RabbitMQ username
- **password**: Your RabbitMQ password  
- **virtualHost**: Your RabbitMQ virtual host (usually starts with `/`)

### 3. Restore Dependencies

```powershell
dotnet restore
```

### 4. Build the Application

```powershell
dotnet build
```

### 5. Complete the Implementation

### 6. Run the Application

```powershell
dotnet run
```

## Dependencies

This project uses:
- **RabbitMQ.Client** (v7.1.2) - Official RabbitMQ client library
- **System.Configuration.ConfigurationManager** (v9.0.9) - For reading App.config