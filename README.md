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

Copy the libe_sqlite3.so to your /usr/local/lib or /usr/lib
```bash
cp libe_sqlite3.so /usr/local/lib
```

Run executable

```bash
LD_LIBRARY_PATH=/usr/local/lib ./Chksum
```

Info

LD_LIBRARY_PATH=/usr/local/lib is needed to tell the executable where the library is located

Alternately you can put the libe_sqlite3.so into the same folder as the executable