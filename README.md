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