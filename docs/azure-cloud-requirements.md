# Minimal Azure VM OS requirements for a **multi-app ASP.NET Minimal API OS**

Your OS is **not a unikernel**, so the minimum feature set is larger. The Azure-facing requirements are mostly unchanged, but the guest OS must now support a **multi-program execution model** capable of loading, isolating, scheduling, and supervising multiple ASP.NET Minimal API applications.

The cleanest minimal target is:

```text
Azure VM
  -> fixed VHD image
  -> custom OS kernel
  -> Hyper-V VMBus
  -> NetVSC networking
  -> DHCP / IPv4 / TCP / UDP
  -> process/app manager
  -> multiple ASP.NET Minimal API apps
  -> preferably Native AOT app binaries
```

ASP.NET Minimal APIs are specifically intended for HTTP APIs with minimal ASP.NET Core dependencies, and Native AOT support includes Minimal APIs, although not every ASP.NET Core feature is AOT-compatible. Microsoft’s ASP.NET Core Native AOT compatibility table lists Minimal APIs as supported, but MVC and Session are not supported. ([Microsoft Learn][1]) ([Microsoft Learn][2])

---

# 0. Recommended implementation assumptions

Use these assumptions unless an agent is explicitly assigned a different target.

## CloudReady claim boundary

For the near-term `SingleMinimalApi` profile, Azure support is not proven by a
local QEMU/libvirt Kestrel smoke. Azure support requires Azure VM evidence from
the Hyper-V device path: fixed VHD or Azure image flow, Hyper-V detection,
VMBus negotiation, NetVSC networking, StorVSC storage, DHCP IPv4, TCP listener
behavior, and Azure serial or boot diagnostic logs. Azure WireServer metadata,
goal-state, and ready-health fixtures are useful later integrations, but they
are not substitutes for a real Azure VM `SingleMinimalApi` Kestrel HTTP
response. `MultiMinimalApi` additionally needs the multi-app
process/isolation/supervisor evidence described later in this document.

| Area               | Minimal production-oriented assumption                              |
| ------------------ | ------------------------------------------------------------------- |
| Azure architecture | x64 Azure VM                                                        |
| Azure image type   | Azure Compute Gallery image                                         |
| Azure OS type      | `Linux`, because Azure image metadata supports `Windows` or `Linux` |
| Azure OS state     | `Specialized`                                                       |
| Azure agent        | Not present                                                         |
| Azure extensions   | Disabled / unsupported                                              |
| VM boot generation | Start with Gen1 BIOS or Gen2 UEFI, but implement only one first     |
| App model          | Multiple isolated application processes                             |
| ASP.NET model      | Prefer **Native AOT Minimal API apps**                              |
| TLS                | Terminate outside the VM initially, or implement TLS later          |
| App deployment     | Bake apps into the image first; add remote deployment later         |

Azure Compute Gallery image definitions require an OS type and OS state; the OS type values are `Windows` or `Linux`, and the OS state can be `Generalized` or `Specialized`. A specialized image already contains a provisioned OS disk, while a generalized image needs provisioning during deployment. ([Microsoft Learn][3])

The OPS/Nanos Azure approach in your attached document is useful as the orchestration model: create Azure images, use Azure Galleries, create instances, configure VPC/subnet/security group behavior, optionally enable IP forwarding or IPv6, and retrieve logs from the serial console. 

---

# 1. Hard minimum Azure VM boot contract

An agent implementing Azure compatibility must satisfy this contract first.

## 1.1 Image artifact

The OS image **MUST** be packaged as a bootable Azure-compatible VHD.

| Requirement    | Implementation requirement                                             |
| -------------- | ---------------------------------------------------------------------- |
| Disk format    | Fixed-size VHD                                                         |
| Alignment      | Virtual size aligned to 1 MiB                                          |
| Image registry | Azure Compute Gallery preferred                                        |
| OS type        | Register as `Linux` unless building an actual Windows-compatible image |
| OS state       | Use `Specialized` first                                                |
| Agent flag     | Disable Azure agent expectations                                       |

Azure Linux image guidance says Azure VHD images must have a virtual size aligned to 1 MB. ([Microsoft Learn][4])

## 1.2 Boot path

Pick **one** initial boot path.

### Option A: Gen1 BIOS path

Minimum:

```text
MBR
BIOS bootloader
x86-64 kernel entry
initial page tables
serial logging
```

This is often simpler for early bring-up.

### Option B: Gen2 UEFI path

Minimum:

```text
GPT
EFI System Partition
UEFI bootloader
x86-64 kernel entry
initial page tables
serial logging
```

This is better for longer-term Azure compatibility, but more work.

## 1.3 Kernel core

The kernel **MUST** implement:

| Kernel feature            | Required behavior                                      |
| ------------------------- | ------------------------------------------------------ |
| x86-64 long mode          | Required for normal Azure x64 VMs                      |
| Physical memory discovery | Parse firmware-provided memory map                     |
| Virtual memory            | Kernel mappings, user mappings, guard pages            |
| Interrupt handling        | IDT, exception handlers, APIC or equivalent            |
| Timer                     | Monotonic clock and preemption timer                   |
| Panic path                | Write panic reason to serial console                   |
| Shutdown/halt             | At least halt loop; clean power control can come later |

---

# 2. Hard minimum Hyper-V / Azure device contract

Azure VMs run on Hyper-V. For a real networked OS, the implementation must support Hyper-V synthetic devices. VMBus is the Hyper-V software construct used to offer synthetic devices such as SCSI controllers, NICs, time sync, shutdown, heartbeat, and KVP exchange to guest VMs. ([Kernel Documentation][5])

## 2.1 Hyper-V detection and setup

The OS **MUST** implement:

```text
Hyper-V CPUID detection
Hyper-V MSR access
hypercall page setup
synthetic interrupt controller setup
VMBus version negotiation
VMBus channel offer processing
VMBus event signaling
VMBus ring-buffer transport
GPADL/shared-memory setup
```

## 2.2 Network device

The OS **MUST** implement the Hyper-V synthetic NIC equivalent of Linux `netvsc`.

Minimum driver behavior:

```text
discover NetVSC device over VMBus
negotiate device protocol version
receive MAC address
send Ethernet frames
receive Ethernet frames
handle link-up/link-down
recover from transient device events
```

Accelerated Networking is **not** part of the minimum. If it is enabled later, custom images need SR-IOV-capable NIC drivers such as MANA or supported Mellanox drivers. ([Microsoft Learn][6])

## 2.3 Storage device

For the earliest boot-only prototype, runtime disk I/O can be avoided if the bootloader loads everything needed into memory.

For this OS, because it runs **multiple ASP.NET apps**, the practical minimum should include storage.

Implement the Hyper-V synthetic SCSI equivalent of Linux `storvsc`.

Minimum storage behavior:

```text
discover StorVSC over VMBus
send basic SCSI commands
read sectors
write sectors
flush writes
detect disk capacity
handle OS disk
optionally handle data disks
```

The app manager needs either:

```text
A. app binaries embedded in a boot RAM image
```

or:

```text
B. app binaries loaded from an OS filesystem
```

For production, use option **B**.

---

# 3. Hard minimum network stack

To run ASP.NET Minimal API apps, the OS must expose working TCP listeners.

## 3.1 Required protocols

The network stack **MUST** implement:

| Layer        | Requirement                                           |
| ------------ | ----------------------------------------------------- |
| Ethernet     | Frame send/receive                                    |
| ARP          | IPv4 neighbor resolution                              |
| IPv4         | Addressing, routing, fragmentation policy             |
| ICMP         | Useful for diagnostics; strongly recommended          |
| UDP          | Required for DHCP and DNS                             |
| DHCP client  | Required for Azure dynamic IP configuration           |
| TCP          | Required for HTTP services                            |
| DNS resolver | Required if apps call named services                  |
| Loopback     | Required for local app-to-app calls and health probes |

Azure’s no-agent Linux guidance states that DHCP is required so the VM can retrieve host IP, DNS resolution, and route management from the virtual network. It also states that generalized no-agent images currently require DHCP-enabled VMs. ([Microsoft Learn][7])

## 3.2 Azure platform IP

The OS should be able to reach:

```text
168.63.129.16
```

Azure uses this address for platform services. Microsoft documents that VM Agent communication uses `168.63.129.16` on ports `80/tcp` and `32526/tcp`, and that the same address can provide DNS services. ([Microsoft Learn][8])

For a specialized image without an agent, this can be minimal at first. For generalized provisioning or deeper Azure integration, it becomes required.

---

# 4. Azure provisioning strategy

## 4.1 Minimal path: specialized image, no agent

Use this path first.

```text
Image:
  osType  = Linux
  osState = Specialized

VM:
  enableAgent = false
  extensions disabled
```

A specialized image avoids the need to implement `cloud-init`, `waagent`, password injection, SSH-key injection, custom data processing, and extension handling.

Microsoft’s no-agent image guidance explicitly says that if no properties need to be set and no provisioning is required, a specialized image should be considered. ([Microsoft Learn][7])

## 4.2 Generalized image path: optional later

Only implement this after the specialized image path works.

For generalized images without an Azure agent, the OS must report ready to Azure.

Minimum WireServer flow:

```text
GET  http://168.63.129.16/machine?comp=goalstate
Parse:
  ContainerId
  InstanceId
  Incarnation

POST http://168.63.129.16/machine?comp=health
Body:
  <Health>
    <GoalStateIncarnation>...</GoalStateIncarnation>
    <Container>
      <ContainerId>...</ContainerId>
      <RoleInstanceList>
        <Role>
          <InstanceId>...</InstanceId>
          <Health>
            <State>Ready</State>
          </Health>
        </Role>
      </RoleInstanceList>
    </Container>
  </Health>
```

Microsoft documents that after networking is configured, a no-agent VM must report ready; failing to report ready can cause Azure to reboot the VM. ([Microsoft Learn][7]) Microsoft also documents the exact goal-state GET and health POST flow against `168.63.129.16`. ([Microsoft Learn][7])

---

# 5. Multi-app OS contract

This is the major correction from the unikernel answer.

The OS must support **multiple independently runnable ASP.NET Minimal API applications**.

## 5.1 Process model

The OS **MUST** implement a process model.

Minimum:

```text
process table
process IDs
parent/child relationship or supervisor ownership
per-process virtual address space
per-process handle table
process exit status
process kill/terminate
process restart
process resource accounting
```

You do **not** need Unix `fork()`.

Recommended minimal process API:

```text
spawn(executable, args, env, workingDirectory, manifest)
exit(code)
kill(pid)
wait(pid)
getpid()
```

Do not implement `fork()` unless specifically needed. ASP.NET Minimal API apps do not require Unix-style process cloning.

## 5.2 Memory isolation

Because this OS runs multiple apps, agents should not assume a single shared app address space.

Minimum:

```text
separate user virtual address space per app process
kernel/user privilege separation
read/write/execute page permissions
guard pages for stacks
copy-in/copy-out validation for syscalls
per-process heap regions
memory limit per app
```

Optional but recommended:

```text
ASLR
W^X enforcement
per-process crash dumps
shared read-only mappings for common runtime files
```

## 5.3 Threading and scheduling

ASP.NET Core uses async I/O, timers, thread-pool execution, and background work. The OS therefore needs real scheduling.

Minimum:

```text
preemptive scheduler
kernel threads or user threads backed by kernel scheduling
thread creation
thread exit
sleep/yield
timer wait
blocking and nonblocking waits
mutex
semaphore
condition variable or futex-style primitive
atomic wait/wake primitive
```

Recommended minimal syscall surface:

```text
thread_create
thread_exit
thread_join
sched_yield
sleep
futex_wait
futex_wake
clock_gettime
timer_create
timer_cancel
```

## 5.4 App lifecycle manager

The OS needs an init/supervisor equivalent.

Minimum responsibilities:

```text
read app manifests
start configured apps
assign ports
set environment variables
set working directories
collect exit codes
restart apps according to policy
stop apps during shutdown
write lifecycle logs to serial and/or file
```

Minimum manifest shape:

```json
{
  "name": "orders-api",
  "executable": "/apps/orders/orders-api",
  "arguments": [],
  "environment": {
    "ASPNETCORE_URLS": "http://0.0.0.0:5001"
  },
  "workingDirectory": "/apps/orders",
  "ports": [5001],
  "restart": "on-failure",
  "memoryLimitBytes": 268435456
}
```

For the first version, each app should listen on a distinct port.

Do **not** implement a reverse proxy first unless required. The minimal routing model is:

```text
app A -> port 5001
app B -> port 5002
app C -> port 5003
Azure NSG/load balancer exposes selected ports
```

---

# 6. ASP.NET Minimal API runtime contract

There are two possible implementation strategies.

## 6.1 Recommended minimum: Native AOT app binaries

This is the smallest realistic path.

Microsoft documents that Native AOT apps are self-contained, compiled ahead of time, do not use a JIT at runtime, and can run on machines without the .NET runtime installed. ([Microsoft Learn][9])

Target model:

```text
Each ASP.NET Minimal API app is published as a Native AOT executable.
The OS loads each app as a separate process.
The app uses the OS syscall/ABI surface for memory, threads, sockets, time, files, and entropy.
```

Benefits:

```text
no CoreCLR installation required
no JIT runtime requirement
smaller app package
faster startup
simpler OS support than full CoreCLR
```

Limitations:

```text
not every ASP.NET Core feature is supported
reflection-heavy libraries may fail
dynamic code generation is unavailable
MVC is not supported in Native AOT
Session is not supported in Native AOT
```

Native AOT support for ASP.NET Core lists Minimal APIs as supported, while several other ASP.NET Core features are unsupported or partially supported. ([Microsoft Learn][2])

## 6.2 Larger path: CoreCLR/JIT runtime

This is **not** the minimum.

Only choose this if the OS must run ordinary framework-dependent ASP.NET apps without Native AOT.

Additional required features:

```text
dynamic loader
shared libraries
JIT executable memory
mmap / mprotect behavior
thread suspension support
GC safepoint integration
exception unwinding
runtime configuration files
assembly loading
dependency resolution
larger filesystem surface
more complete libc/POSIX or .NET PAL port
```

For AI agents implementing the first Azure-compatible version, avoid this path.

---

# 7. Minimal OS ABI for ASP.NET Minimal API apps

Whether the OS exposes Linux-like syscalls or a custom ABI, ASP.NET apps need the following capabilities.

## 7.1 Process and environment

Required:

```text
get executable path
read argv
read environment variables
get process ID
exit process
spawn process
wait for process
```

## 7.2 Memory

Required:

```text
reserve virtual memory
commit memory
release memory
change page permissions
query page size
allocate thread stacks
provide guard pages
```

Minimum APIs:

```text
vm_reserve
vm_commit
vm_release
vm_protect
get_page_size
```

## 7.3 Threads and synchronization

Required:

```text
create thread
join thread
yield
sleep
monotonic clock
wall clock
atomic wait/wake
mutex-compatible primitive
condition/event-compatible primitive
```

Minimum APIs:

```text
thread_create
thread_join
thread_exit
sched_yield
clock_monotonic
clock_realtime
futex_wait
futex_wake
```

## 7.4 Filesystem

For a baked-image first version, keep this small.

Required:

```text
open
read
write
close
stat
directory enumerate
file exists
read config files
write logs
```

Recommended filesystem model:

```text
read-only app filesystem
writable /var/log
writable /tmp
optional writable app data volume
```

Minimum paths:

```text
/apps
/etc/os-apps
/var/log
/tmp
```

## 7.5 Networking and sockets

Kestrel needs real socket support.

Required:

```text
socket
bind
listen
accept
connect
send
recv
close
setsockopt
getsockopt
nonblocking mode
poll/epoll/completion-style async wait
DNS resolution
```

Minimum socket support:

```text
AF_INET
SOCK_STREAM
SOCK_DGRAM
TCP_NODELAY
SO_REUSEADDR
nonblocking TCP accept/read/write
UDP send/receive
```

Recommended async I/O API:

```text
io_poll_create
io_poll_add
io_poll_remove
io_poll_wait
```

or a completion-port style equivalent.

The exact shape can be custom, but the .NET/Kestrel port must be able to express:

```text
many concurrent connections
nonblocking accept
nonblocking read/write
timers
cancellation
backpressure
connection close notification
```

## 7.6 Entropy and crypto

Required:

```text
secure random bytes
```

Minimum:

```text
getrandom(buffer)
```

This is needed by .NET, TLS, auth tokens, GUIDs, and cryptographic libraries.

## 7.7 Time

Required:

```text
monotonic time
wall-clock time
timer queue
timezone optional
```

Minimum:

```text
clock_gettime_monotonic
clock_gettime_utc
timer_wait
```

Wall-clock accuracy can initially come from Hyper-V time services, Azure platform time, or a simple boot-time source plus network correction.

---

# 8. HTTP, HTTPS, and TLS

For the absolute minimum, run apps over HTTP inside the VM/network:

```text
http://0.0.0.0:5001
http://0.0.0.0:5002
```

Terminate HTTPS at one of:

```text
Azure Application Gateway
Azure Front Door
external reverse proxy
separate TLS terminator service
```

This avoids needing a full certificate store and TLS stack inside the custom OS for version 1.

If the apps must serve HTTPS directly, add:

```text
TLS library port
certificate loading
private-key storage
certificate chain validation
system trust store
SNI
ALPN if HTTP/2 is needed
```

HTTP/1.1 is the minimum. HTTP/2, HTTP/3, gRPC, and WebSockets are optional feature tracks.

---

# 9. Logging and diagnostics

This OS needs strong early diagnostics because Azure custom OS debugging is otherwise painful.

## 9.1 Serial console

The OS **MUST** write boot and panic logs to COM1 / serial console.

Minimum log events:

```text
kernel entry
memory map parsed
paging initialized
interrupts initialized
Hyper-V detected
VMBus initialized
NetVSC initialized
DHCP lease acquired
app manager started
each app started
each app bound to port
panic / exception / app crash
```

Your OPS/Nanos document specifically uses `ops instance logs` to retrieve serial-console logs from Azure storage, so serial output should be treated as a required debugging surface. 

## 9.2 App logs

Minimum:

```text
stdout/stderr equivalent per app
append-only log files
serial mirror for supervisor/app startup failures
```

Recommended:

```text
/apps/<name>/logs
/var/log/apps/<name>.log
/var/log/system.log
```

---

# 10. Security minimum

Because the OS runs multiple apps, it needs basic containment.

## 10.1 Required

```text
kernel/user separation
per-process address spaces
validated syscall pointers
non-executable user stacks by default
per-app memory limits
per-app port ownership
process termination on illegal memory access
kernel panic isolation where possible
```

## 10.2 Strongly recommended

```text
W^X memory policy
ASLR
read-only app code mappings
capability-style permissions
per-app filesystem permissions
per-app network bind permissions
crash restart policy
```

## 10.3 Not required for first Azure boot

```text
users/groups
password login
SSH
shell
sudo
package manager
containers
SELinux/AppArmor equivalent
```

---

# 11. Azure networking configuration surface

The OS must cooperate with Azure networking but does not need to create Azure resources itself.

The deployment layer handles:

```text
resource group
storage account
Azure Gallery
VNet/VPC equivalent
subnet
security group / NSG
public IP
IP forwarding option
IPv6 option
```

The guest OS handles:

```text
DHCP
IP address assignment
default route
DNS resolver
TCP listeners
optional IPv6 stack
optional packet forwarding
```

OPS exposes Azure VPC/subnet/security group selection, IP forwarding, and IPv6 enablement in configuration; those flags only become useful if the guest OS implements the corresponding networking behavior. 

---

# 12. Features that are still not required

Even though this is not a unikernel, the OS still does **not** need to be a Linux clone.

You can skip these initially:

| Feature                  | Reason                                                         |
| ------------------------ | -------------------------------------------------------------- |
| Full POSIX               | Only the .NET/app ABI surface is required                      |
| `fork()`                 | Use `spawn()` instead                                          |
| Shell                    | Not required for app execution                                 |
| SSH                      | Not required if apps are baked into image and serial logs work |
| `systemd`                | Implement your own app supervisor                              |
| Azure Linux Agent        | Avoid with specialized image                                   |
| `cloud-init`             | Avoid with specialized image                                   |
| VM extensions            | Avoid initially                                                |
| Docker/container runtime | You already have an OS-level app manager                       |
| GUI/framebuffer          | Not needed                                                     |
| Full user/group model    | Optional later                                                 |
| Package manager          | Optional later                                                 |
| IPv6                     | Optional unless explicitly enabled                             |
| Accelerated Networking   | Optional performance feature                                   |

---

# 13. Required implementation work packages for AI agents

Use these as agent-deliverable modules.

## Agent A — Azure image and boot

**Goal:** Produce a VHD that Azure can boot.

Deliverables:

```text
fixed VHD builder
1 MiB size alignment
boot partition layout
bootloader integration
kernel load protocol
Azure Compute Gallery metadata instructions
specialized image deployment instructions
```

Acceptance tests:

```text
VHD uploads to Azure successfully
Azure image version can be created
VM reaches kernel entry
serial console prints "kernel started"
```

---

## Agent B — Kernel core

**Goal:** Bring up the kernel substrate.

Deliverables:

```text
x86-64 long mode
memory map parser
page allocator
virtual memory manager
kernel heap
IDT/exceptions
interrupt controller
timer
panic logger
serial console driver
```

Acceptance tests:

```text
intentional page fault reports fault address
timer interrupt fires repeatedly
kernel can allocate/free pages
panic path writes to serial
```

---

## Agent C — Hyper-V VMBus

**Goal:** Discover Azure synthetic devices.

Deliverables:

```text
Hyper-V detection
hypercall setup
SynIC setup
VMBus negotiation
channel offer handling
ring buffer implementation
GPADL/shared-memory implementation
device registry
```

Acceptance tests:

```text
OS logs Hyper-V detected
OS logs VMBus connected
OS lists synthetic NIC channel
OS lists synthetic SCSI channel if present
```

---

## Agent D — NetVSC and network stack

**Goal:** Get an Azure IP and accept TCP connections.

Deliverables:

```text
NetVSC driver
Ethernet layer
ARP
IPv4
ICMP
UDP
DHCP client
TCP stack
DNS resolver
socket API
nonblocking I/O readiness API
```

Acceptance tests:

```text
DHCP lease acquired
default route installed
DNS server configured
TCP listener accepts connection
HTTP test app returns response
multiple simultaneous TCP clients work
```

---

## Agent E — StorVSC and filesystem

**Goal:** Load multiple app packages and write logs.

Deliverables:

```text
StorVSC driver
block cache
partition parser
minimal filesystem
read-only app area
writable log/tmp area
optional data disk support
```

Acceptance tests:

```text
OS reads app manifest from disk
OS loads app executable from disk
OS writes app logs
OS survives reboot with persistent logs if writable disk enabled
```

---

## Agent F — Process and memory isolation

**Goal:** Run multiple independent apps.

Deliverables:

```text
process table
per-process address spaces
ELF/PE/native executable loader
user-mode entry
syscall dispatcher
handle table
process exit/wait/kill
resource limits
```

Acceptance tests:

```text
two apps run concurrently
crashing one app does not crash the OS
crashing one app does not corrupt another app
supervisor observes app exit code
memory limit prevents runaway allocation
```

---

## Agent G — Threading and async runtime primitives

**Goal:** Support ASP.NET Core concurrency.

Deliverables:

```text
preemptive scheduler
thread_create/thread_join
sleep/yield
monotonic clock
wall clock
futex or equivalent
timer queue
async I/O wait primitive
```

Acceptance tests:

```text
thread pool stress test passes
timers fire accurately enough
1000 concurrent idle connections do not block kernel
cancellation wakes blocked waits
```

---

## Agent H — ASP.NET Minimal API app ABI

**Goal:** Define and implement the OS ABI needed by the .NET app target.

Preferred deliverable:

```text
Native AOT ASP.NET Minimal API app target
static native executable format
minimal OS syscall bindings
Kestrel-compatible socket integration
filesystem bindings
clock bindings
entropy binding
thread/futex bindings
```

Acceptance tests:

```text
Native AOT Minimal API app starts
MapGet endpoint returns response
two Minimal API apps run on different ports
app can read config file
app can write log file
app can make outbound HTTP request if DNS is configured
```

---

## Agent I — App supervisor

**Goal:** Start, monitor, and restart multiple apps.

Deliverables:

```text
manifest parser
app dependency ordering
environment variable injection
port reservation
process spawn
stdout/stderr capture
restart policy
shutdown policy
health state
```

Acceptance tests:

```text
three apps start from manifests
port conflict is detected
failed app restarts according to policy
disabled app is not started
supervisor logs app lifecycle events
```

---

## Agent J — Azure no-agent readiness

**Goal:** Optional support for generalized images or cleaner Azure readiness signaling.

Deliverables:

```text
HTTP client capable of reaching 168.63.129.16
WireServer goal-state GET
XML parser or minimal extractor
WireServer health POST
Ready-state report
```

Acceptance tests:

```text
GET /machine?comp=goalstate succeeds
ContainerId/InstanceId/Incarnation parsed
POST /machine?comp=health succeeds
Azure marks VM provisioned without waagent
```

This work package is optional if the first release uses only specialized images.

---

# 14. Minimum end-to-end milestone sequence

## Milestone 1 — Azure boot

```text
Azure VM boots custom VHD
kernel writes serial logs
no networking required
```

## Milestone 2 — VMBus discovered

```text
Hyper-V detected
VMBus connected
NIC and disk channels discovered
```

## Milestone 3 — Network online

```text
NetVSC works
DHCP succeeds
TCP listener accepts connection
```

## Milestone 4 — Single app

```text
one Native AOT ASP.NET Minimal API app starts
GET / returns response
serial/app logs show lifecycle
```

## Milestone 5 — Multiple apps

```text
two or more ASP.NET Minimal API apps start
each binds a separate port
apps run in separate processes
one app crash does not affect others
```

## Milestone 6 — Storage-backed app loading

```text
app manifests loaded from disk
app binaries loaded from disk
logs written to disk
```

## Milestone 7 — Azure operational readiness

```text
specialized image deployment repeatable
agent disabled
extensions disabled
serial logs retrievable
optional WireServer Ready implemented
```

---

# 15. Absolute minimal final checklist

For this specific OS, the minimum viable Azure target is:

```text
[Azure image]
- fixed VHD
- 1 MiB aligned virtual size
- Azure Compute Gallery image
- osType = Linux
- osState = Specialized
- Azure agent disabled

[Boot/kernel]
- BIOS or UEFI boot path
- x86-64 kernel
- paging
- interrupts
- timer
- serial console

[Hyper-V/Azure devices]
- Hyper-V detection
- VMBus
- NetVSC synthetic NIC
- DHCP
- IPv4
- ARP
- UDP
- TCP
- DNS
- StorVSC strongly recommended for multi-app loading

[Multi-app OS]
- process model
- per-process address spaces
- executable loader
- syscall ABI
- preemptive scheduler
- threads
- futex/sync primitives
- app supervisor
- app manifests
- logging

[ASP.NET Minimal API]
- preferably Native AOT executables
- socket API
- nonblocking async I/O primitive
- timers
- filesystem read/write
- environment variables
- secure random
- clock APIs

[Not required initially]
- Linux kernel
- full POSIX
- fork
- shell
- SSH
- systemd
- Docker
- cloud-init
- Azure Linux Agent
- Azure VM extensions
- Accelerated Networking
```

[1]: https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0 "Tutorial: Create a Minimal API with ASP.NET Core | Microsoft Learn"
[2]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-10.0 "ASP.NET Core support for Native AOT | Microsoft Learn"
[3]: https://learn.microsoft.com/en-us/rest/api/compute/gallery-images/create-or-update?view=rest-compute-2025-11-01 "Gallery Images - Create Or Update - REST API (Azure Compute) | Microsoft Learn"
[4]: https://learn.microsoft.com/en-us/azure/virtual-machines/linux/create-upload-generic "Prepare Linux for imaging - Azure Virtual Machines | Microsoft Learn"
[5]: https://docs.kernel.org/virt/hyperv/vmbus.html "VMBus — The Linux Kernel  documentation"
[6]: https://learn.microsoft.com/en-us/azure/virtual-network/accelerated-networking-overview "Azure Accelerated Networking Overview and Benefits | Microsoft Learn"
[7]: https://learn.microsoft.com/en-us/azure/virtual-machines/linux/no-agent "Create Linux images without a provisioning agent - Azure Virtual Machines | Microsoft Learn"
[8]: https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16 "Azure IP Address 168.63.129.16 Overview | Microsoft Learn"
[9]: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ "Native AOT deployment overview - .NET | Microsoft Learn"
