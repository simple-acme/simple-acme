# Arsys/ServidoresDNS SOAP Client for .NET

This project provides a functional SOAP client for the Arsys DNS API (servidoresdns.net), specifically tailored for modern .NET environments (Core, 5, 6, 7, 8+).

## ⚠️ Technical Challenges Resolved

The Arsys API uses legacy technologies that require specific configuration in modern .NET:
- **Encoding:** It uses `ISO-8859-1` (Latin-1) instead of the standard UTF-8.
- **Protocol:** SOAP RPC/Encoded (causes class name collisions during generation).
- **Authentication:** Requires forced "Basic Auth" (Pre-authentication) on the very first request.

---

## 🚀 Client Generation Guide

### 1. Preparation
Download the WSDL locally to avoid SSL handshake errors during code generation. This is the most reliable method for this specific API:
```powershell
Invoke-WebRequest "[https://api.servidoresdns.net:54321/hosting/api/soap/index.php?wsdl](https://api.servidoresdns.net:54321/hosting/api/soap/index.php?wsdl)" -OutFile ArsysDNS.wsdl

```

### 2. Generate the Client

You can generate the proxy classes using either the Command Line or the Visual Studio GUI.

#### Option A: Command Line (dotnet-svcutil)

Install the tool and run the generation command:

```powershell
dotnet tool install --global dotnet-svcutil

dotnet-svcutil .\ArsysDNS.wsdl `
    --outputDir ./ `
    --outputFile ArsysDNSClient.cs `
    --namespace "*,PKISharp.WACS.Plugins.ValidationPlugins.Dns.Arsys"

```

#### Option B: Visual Studio GUI (Connected Services)

1. Right-click your project in **Solution Explorer**.
2. Select **Add** > **Connected Service**.
3. Choose **Microsoft WCF Web Service Reference Provider**.
4. Click **Browse** and select the local `ArsysDNS.wsdl` file you downloaded.
5. Set the Namespace to `PKISharp.WACS.Plugins.ValidationPlugins.Dns.Arsys`.
6. Complete the wizard to generate the `Reference.cs` file.

---

## 🛠️ Required Configuration (Post-Generation)

To make the client functional, you must include the extension classes for **Custom Encoding** (`CustomTextMessageXXXX`) and the **Authentication Inspector** (`BasicAuthMessageInspector`) in your project.

---

## 📝 Debugging

To inspect the objects received in the console, it is highly recommended to serialize them to JSON:

```csharp
Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));

```


