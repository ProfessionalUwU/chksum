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
dotnet build chksum.csproj
```

Publish project

```bash
dotnet publish --configuration Release chksum.csproj
```

Go to the publish folder
```bash
cd bin/Release/net7.0/linux-x64/publish
```

Run executable

```bash
./chksum
```
