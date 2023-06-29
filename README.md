# chksum

Checksums every file under the current directory

## Run Locally

Clone the project

```bash
git clone https://gitea.hopeless-cloud.xyz/ProfessionalUwU/chksum.git
```

Go to the project directory

```bash
cd chksum
```

Install dependencies

```bash
pacman -S dotnet-runtime dotnet-sdk
```

Build project

```bash
just build
```

Publish project

```bash
just publish
```

Go to the publish folder
```bash
cd src/Chksum/bin/Release/net7.0/linux-x64/publish
```

Run executable

```bash
./Chksum
```

## Enabling verbose output for troubleshooting

1. Open the file called chksum.cs with your editor of choice.
2. At the top there will be the logger configuration which you can change. Should look like this.
```cs
private ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Error)
            .WriteTo.File("chksum.log")
            .CreateLogger();
```
3. Change the minimum level of the logger to Verbose.
4. Compile the program
5. Profit. Now you will be able to see how what the program is doing in detail.