# OSProj
# Part A of the project is in the " MyProject folder" while Part B is in the " PartB" folder

## Overview
This project demonstrates fundamental concepts of Operating Systems development, focusing on two key areas:

### Multi-Threading Implementation
A banking simulation that demonstrates:
- Thread creation
- Synchronization
- Deadlock scenarios
- Deadlock resolution through a four-phase development approach

### Inter-Process Communication (IPC)
A socket-based communication system that:
- Allows processes to exchange structured data
- Ensures data integrity validation
- Implements error handling


## Installation Instructions
### Install .NET SDK:
#### For Ubuntu/Debian
```sh
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```
#### Verify installation
```sh
dotnet --version
```

### Clone the repository:
```sh
Then do:
cd Part_A
cd Myproject
For PartA
And:
cd Part_B
cd IPC
for PartB
```

### Build the project:
```sh
dotnet build
```
## Running the Programs
### Multi-Threading Implementation (Part_A)
Run the banking simulation with all four phases:
```sh
cd Myproject
dotnet run
the test will be shown in the code.
```
### Inter-Process Communication (Part_B)
To run the IPC demonstration with two separate processes:

#### Terminal 1 (Consumer):
```sh
cd IPC
dotnet run -- consumer (Run Producer first)
```
#### Terminal 2 (Producer):
```sh
cd IPC
dotnet run -- producer
```


To run the error handling validation test:
```sh
cd IPC
cd ErrorHandlingTest
cd ErrorHandlingTest
then do whats shown for the main test above.
To run the data integrity test:
```sh
cd IPC
cd DataIntegrityTest
cd DataIntegrityTest
then run the producer and consumer commands like shown for the main test above.


# Dependencies/ Installation guide

## Install Windows Subsystem for Linux (WSL)
- **Windows Users:**  
  - Open **Settings** → **Optional Features**  
  - Enable **Windows Subsystem for Linux (WSL)** and **Virtual Machine Platform**  

## Install Ubuntu  
- Open **Microsoft Store** and search for **Ubuntu**  
- Download and install it as an extension  

## Install Visual Studio Code  
- Download VS Code from the [official website](https://code.visualstudio.com/)  
- Install the `.deb` package  
- Run the following command in Ubuntu:  
  ```sh
  sudo apt install ./.deb
  
##  Install .NET SDK  
- **Download** the SDK from the [official .NET website](https://dotnet.microsoft.com/en-us/download)  
- **Install** it by running the following command in Ubuntu:  
  ```sh
  sudo apt install dotnet-sdk-8.0

  ##  Install Required Extensions in VS Code  
- Open **Visual Studio Code**  
- Go to **Extensions** (Ctrl + Shift + X)  
- Search for and **install** the following:  
  - 🔹 **C#**  
  - 🔹 **C# Dev Kit**  
  - 🔹 **.NET SDK**  

These extensions are **mandatory** for development.  

