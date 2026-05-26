# AWS EC2 minimal OS requirements for a **multi-app ASP.NET Minimal API OS**

For AWS, the smallest sane target is:

```text
AWS EC2
  -> HVM AMI
  -> EBS-backed root volume
  -> custom OS kernel
  -> Nitro-compatible device support
  -> ENA networking
  -> NVMe storage
  -> DHCP / IPv4 / TCP / UDP / DNS
  -> process/app manager
  -> multiple ASP.NET Minimal API apps
  -> preferably Native AOT app binaries
```

The major AWS difference from Azure is the device model. On Azure, the key device substrate is **Hyper-V VMBus**. On AWS Nitro, the key device substrate is **PCIe + ENA + NVMe**. AWS documentation states that current-generation EC2 instance types support **HVM only**, and Nitro-based storage exposes EBS and instance-store volumes as **NVMe block devices**. Nitro networking uses **ENA** for enhanced networking. ([AWS Documentation][1])

Your attached OPS/Nanos AWS document is the orchestration model: OPS creates/uploads AWS images, uses an S3 bucket for AMI creation, automates the `vmimport` role, creates instances from images, exposes ports, configures VPC/security groups/static IPs, retrieves serial logs, and supports CloudWatch logging.    

---

# 0. Recommended implementation assumptions

Use these assumptions for the first AWS implementation unless an agent is explicitly assigned a different target.

## CloudReady claim boundary

For the near-term `SingleMinimalApi` profile, AWS support is not proven by a
local QEMU/libvirt Kestrel smoke. AWS support requires EC2/Nitro evidence from
the AWS device path: HVM image or AMI import/registration, ENA networking, NVMe
root storage, DHCP IPv4, TCP listener behavior, and EC2 serial logs. IMDSv2,
user-data, IAM credential, and cloud-init-free provisioning work are useful
later integrations, but they are not substitutes for a real AWS Nitro
`SingleMinimalApi` Kestrel HTTP response. `MultiMinimalApi` additionally needs
the multi-app process/isolation/supervisor evidence described later in this
document.

Summary: AWS `SingleMinimalApi` requires ENA networking, NVMe root storage, DHCP IPv4, TCP listener behavior, and EC2 serial-log evidence.

| Area               | Minimal production-oriented assumption                            |
| ------------------ | ----------------------------------------------------------------- |
| AWS platform       | EC2 Nitro                                                         |
| Architecture       | `x86_64` first                                                    |
| AMI virtualization | HVM                                                               |
| Root device        | EBS-backed root volume                                            |
| Boot mode          | Legacy BIOS first **or** UEFI first; implement only one initially |
| Network device     | ENA                                                               |
| Storage device     | NVMe                                                              |
| App model          | Multiple isolated application processes                           |
| ASP.NET model      | Prefer **Native AOT Minimal API apps**                            |
| TLS                | Terminate outside the VM initially                                |
| Deployment         | Bake apps into AMI first; add dynamic deployment later            |
| AWS agents         | No SSM agent, no CloudWatch agent, no `cloud-init` initially      |

For x64 Nitro, this gives the cleanest first target:

```text
EC2 Nitro x86_64
  HVM AMI
  EBS root
  ENA NIC
  NVMe root disk
  DHCP IPv4
  Native AOT ASP.NET Minimal API apps
```

Do **not** start by supporting all EC2 families. Do **not** start with Xen, Graviton, multiple ENIs, IPv6, IMDS credentials, SSM, or CloudWatch. Add those as separate tracks.

---

# 1. AWS AMI / image contract

An agent implementing AWS image support must satisfy this contract first.

## 1.1 AMI artifact

The bootable artifact on AWS is an **AMI**, not an Azure Gallery image.

AWS describes an AMI as the image that provides the information required to launch an EC2 instance, including the software needed to boot and the block-device mapping. AMIs are tied to Region, OS, processor architecture, root volume type, and virtualization type. ([AWS Documentation][2])

Minimum AMI requirements:

```text
virtualization type: hvm
architecture: x86_64 first
root device: EBS-backed
root volume: bootable OS image
networking: ENA support enabled for Nitro target
boot mode: legacy-bios or uefi, matching the actual OS boot layout
```

## 1.2 VM import / image upload path

If using the OPS/Nanos-style path, agents need:

```text
AWS credentials
S3 bucket for image upload / AMI creation
vmimport role tied to that bucket
CloudConfig.BucketName
CloudConfig.Zone
```

The attached OPS/Nanos AWS document explicitly lists AWS credentials and an S3 bucket as prerequisites, and shows `CloudConfig.Zone` plus `CloudConfig.BucketName` for image creation. 

AWS VM Import/Export supports several image formats, including OVA, VMDK, VHD/VHDX, and raw images. It also supports both UEFI and Legacy BIOS boot modes when importing, but the image’s boot layout must actually match the selected boot mode. ([AWS Documentation][3])

## 1.3 ARM / Graviton warning

Treat Graviton as a **separate implementation track**.

Your OPS/Nanos document says Nanos supports AWS Graviton 2 and 3 and SMP.  However, AWS VM Import/Export documentation says VMs using ARM64 architecture are not currently supported by VM Import/Export. ([AWS Documentation][3])

Agent instruction:

```text
Do not assume the x86_64 import-image pipeline works for Graviton.
For Graviton, validate the exact AMI creation path separately.
```

---

# 2. Boot contract

The OS must boot as an EC2 HVM guest.

## 2.1 Boot path options

Pick **one** initial boot path.

### Option A — x86_64 Legacy BIOS path

Minimum:

```text
MBR
BIOS-compatible bootloader
x86-64 kernel entry
initial page tables
ACPI discovery
serial logging
```

This is usually the simplest first target for x86_64.

### Option B — x86_64 UEFI path

Minimum:

```text
GPT
EFI System Partition
fallback UEFI loader
/EFI/BOOT/BOOTX64.EFI
x86-64 kernel entry
ACPI discovery
serial logging
```

For UEFI Linux imports, AWS requires a fallback EFI binary at `/EFI/BOOT/BOOTX64.EFI`. ([AWS Documentation][4])

### Option C — Graviton / ARM64 UEFI path

Minimum:

```text
aarch64 kernel
UEFI boot path
GIC interrupt controller
ARM timer
ACPI or device-table support as required
ENA
NVMe
```

Do this after x86_64 Nitro works.

## 2.2 Boot mode metadata

AWS boot behavior depends on the AMI boot mode, OS boot support, and instance type. AWS documents AMI boot modes such as `legacy-bios`, `uefi`, and `uefi-preferred`, and notes that Nitro instances generally support both UEFI and Legacy BIOS, with exceptions. ([AWS Documentation][5])

Agent instruction:

```text
The AMI boot-mode setting does not make the OS bootable.
The disk layout, bootloader, firmware path, and AMI boot mode must match.
```

---

# 3. Kernel core contract

The kernel **MUST** implement:

| Kernel feature   | Required behavior                                      |
| ---------------- | ------------------------------------------------------ |
| CPU mode         | `x86_64` long mode first; `aarch64` later for Graviton |
| Memory discovery | Parse firmware/ACPI-provided memory map                |
| Virtual memory   | Kernel mappings, user mappings, guard pages            |
| Interrupts       | IDT/APIC for x86_64; GIC for ARM64                     |
| Timer            | Monotonic clock and preemption timer                   |
| ACPI             | Required for modern EC2 platform discovery             |
| PCI/PCIe         | Required for Nitro ENA/NVMe device discovery           |
| DMA safety       | Required for ENA/NVMe buffers                          |
| Serial console   | Required for early diagnostics                         |
| Panic path       | Write fault reason and registers to serial             |
| Shutdown/halt    | At least halt loop; clean power control later          |

Minimum kernel boot log:

```text
kernel entry
memory map parsed
paging initialized
ACPI initialized
PCI bus enumerated
interrupts initialized
timer initialized
serial console online
```

---

# 4. AWS Nitro device contract

For a practical AWS EC2 OS, target **Nitro first**.

Do **not** implement Hyper-V VMBus for AWS.
Do **not** assume virtio.
Do **not** assume Linux drivers.

For Nitro, implement:

```text
ACPI
PCI/PCIe enumeration
MSI/MSI-X or interrupt delivery needed by devices
ENA network driver
NVMe block driver
serial console output
```

## 4.1 ENA network driver

The OS **MUST** implement the AWS Elastic Network Adapter equivalent.

AWS documentation states that enhanced networking uses SR-IOV, that ENA supports high-throughput networking, and that all Nitro-based instances use ENA. ([AWS Documentation][6]) AWS also states that AMIs must include the required ENA driver to use enhanced networking. ([AWS Documentation][7])

Minimum ENA behavior:

```text
discover ENA PCI device
initialize admin queue
initialize transmit queues
initialize receive queues
configure interrupts
obtain MAC address
send Ethernet frames
receive Ethernet frames
handle link state
recover from transient reset/error conditions
```

Acceptance target:

```text
EC2 instance receives DHCP lease over ENA
OS can accept TCP connection on exposed port
```

## 4.2 NVMe block driver

The OS **MUST** implement NVMe for Nitro storage.

AWS Nitro exposes EBS and instance-store volumes as NVMe block devices. ([AWS Documentation][8]) AWS also recommends EBS-backed AMIs for faster launch and persistent storage, and each launched instance has a single root volume containing the boot image. ([AWS Documentation][9])

Minimum NVMe behavior:

```text
discover NVMe PCI device
create admin queue
identify controller
identify namespace
read sectors
write sectors
flush writes
handle completion queues
handle errors/timeouts
support EBS root volume
support optional data volumes
```

For your multi-app OS, NVMe is practically required because apps, manifests, config, logs, and optional app data need persistent storage.

---

# 5. Optional Xen compatibility track

Do **not** make Xen the first production target.

Your OPS/Nanos document says Nanos supports older instance types such as `t2` as well as newer Nitro types such as `t3` and `c5`.  For your OS, support this only as an optional compatibility track.

If supporting older non-Nitro EC2 instances, agents may need:

```text
Xen HVM platform support
Xen-compatible console behavior
Xen block device support
Xen network path or legacy AWS device support
```

Recommended rule:

```text
First release: Nitro only.
Compatibility release: add Xen if required.
```

---

# 6. AWS network stack contract

To run ASP.NET Minimal API apps, the OS must expose working TCP listeners.

## 6.1 Required protocols

The OS **MUST** implement:

| Layer        | Requirement                                           |
| ------------ | ----------------------------------------------------- |
| Ethernet     | Frame send/receive over ENA                           |
| ARP          | IPv4 neighbor resolution                              |
| IPv4         | Addressing, routing, fragmentation policy             |
| ICMP         | Strongly recommended for diagnostics                  |
| UDP          | Required for DHCP and DNS                             |
| DHCP client  | Required for normal VPC IP assignment                 |
| TCP          | Required for HTTP services                            |
| DNS resolver | Required for outbound calls by apps                   |
| Loopback     | Required for local app-to-app calls and health checks |

AWS VPC networking uses DHCP, and AWS documentation states that EC2 applications can use Amazon DHCP to retrieve IP leases, DNS, and router configuration. ([AWS Documentation][10]) AWS DHCP option sets also provide Amazon DNS and Amazon Time Sync settings. ([AWS Documentation][11])

## 6.2 Security groups and exposed ports

The guest OS does not create security groups. The deployment layer does.

Your OPS/Nanos document shows exposing ports through CLI/config and selecting VPC/security group values through `CloudConfig.VPC` and `CloudConfig.SecurityGroup`. 

Minimum guest behavior:

```text
bind TCP port
listen
accept
read
write
close
```

Minimum deployment behavior:

```text
security group allows inbound app ports
instance has public IP, Elastic IP, or load balancer path if internet-facing
app binds 0.0.0.0, not only loopback
```

AWS security groups are stateful virtual firewall rules controlling inbound and outbound traffic to instances. ([AWS Documentation][12])

## 6.3 Private static IP

Default first target:

```text
Use DHCP.
```

OPS/Nanos defaults to DHCP for AWS and allows a static private IP through `RunConfig.IPAddress` if the address is available inside the chosen/default VPC. 

Static IP support is optional for the OS if the deployment pipeline handles IP assignment at the ENI/VPC layer and DHCP returns the assigned address.

## 6.4 IPv6

IPv6 is optional for the first release.

Your OPS/Nanos document says AWS IPv6 uses DHCPv6, and the subnet must have IPv6 enabled. 

If implementing IPv6, add:

```text
IPv6 addressing
ICMPv6
Neighbor Discovery
DHCPv6
AAAA DNS support
dual-stack socket behavior
```

---

# 7. EC2 metadata, user data, and IAM credentials

This is **not required for first boot**, but it becomes important once apps need AWS SDK access, region discovery, instance identity, user-data config, or IAM role credentials.

## 7.1 IMDS endpoint

AWS instance metadata is available from inside the instance. The IPv4 endpoint is:

```text
http://169.254.169.254/latest/meta-data/
```

On Nitro instances in IPv6-enabled subnets, AWS also documents an IPv6 metadata endpoint:

```text
http://[fd00:ec2::254]/latest/meta-data/
```

AWS documents these metadata endpoints and the IMDSv2 token flow. ([AWS Documentation][13])

## 7.2 IMDSv2

Implement IMDSv2 before exposing metadata to apps.

Minimum IMDSv2 flow:

```text
PUT  /latest/api/token
  header: X-aws-ec2-metadata-token-ttl-seconds

GET  /latest/meta-data/...
  header: X-aws-ec2-metadata-token
```

AWS documents IMDSv2 token requirements and the metadata options that can make tokens required or optional. ([AWS Documentation][13])

## 7.3 Multi-app security rule

Because this OS runs **multiple apps**, do **not** blindly expose IMDS to every app.

Minimum policy:

```text
default: apps cannot call 169.254.169.254 directly
supervisor may fetch metadata
supervisor may pass selected config to apps
IAM credentials are exposed only to apps that need them
metadata responses are cached where appropriate
```

Reason: on EC2, instance-profile credentials are tied to the instance. Without OS-level mediation, every app on the VM can potentially access the same role credentials.

---

# 8. AWS provisioning strategy

## 8.1 Minimal path: baked AMI, no agent

Use this path first.

```text
AMI contains:
  custom OS
  app supervisor
  app manifests
  app binaries
  required config

EC2 launch:
  no cloud-init dependency
  no SSM agent dependency
  no CloudWatch agent dependency
  no SSH dependency
```

AWS does not require your custom OS to be Linux, run `systemd`, run `cloud-init`, or run the SSM agent just to boot an AMI. The AMI must be compatible with the selected instance architecture, virtualization type, boot mode, and root-device mapping. ([AWS Documentation][2])

## 8.2 Optional path: user-data processing

Add later if required.

AWS user data is available through instance metadata, and AWS documents size/encoding behavior for user data. ([AWS Documentation][13])

Minimum user-data support:

```text
fetch IMDSv2 token
fetch /latest/user-data
base64 decode if needed by pipeline
parse custom config format
pass selected values to supervisor
```

Do not implement general `cloud-init`. Implement only the config semantics your OS needs.

## 8.3 Optional path: SSM-like management

Not required initially.

If remote management is needed later, either:

```text
port AWS Systems Manager agent semantics
```

or:

```text
build a custom management agent for your OS
```

For the first AWS-compatible release, avoid this.

---

# 9. Multi-app OS contract

This is the same architectural correction as the Azure version: your OS is **not** a unikernel. It must support multiple independent ASP.NET Minimal API applications.

## 9.1 Process model

The OS **MUST** implement:

```text
process table
process IDs
supervisor-owned app processes
per-process virtual address space
per-process handle table
process exit status
process kill/terminate
process restart
process resource accounting
```

Recommended minimal process API:

```text
spawn(executable, args, env, workingDirectory, manifest)
exit(code)
kill(pid)
wait(pid)
getpid()
```

Do **not** implement Unix `fork()` first. ASP.NET Minimal API apps do not require process cloning. Use `spawn()`.

## 9.2 Memory isolation

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

Recommended:

```text
ASLR
W^X enforcement
read-only mappings for app code
per-process crash reports
shared read-only runtime/config mappings
```

## 9.3 Scheduling and threading

ASP.NET Core uses async I/O, timers, background work, and thread-pool execution. The OS needs real concurrency.

Minimum:

```text
preemptive scheduler
thread creation
thread exit
thread join
sleep/yield
timer wait
blocking and nonblocking waits
mutex-compatible primitive
semaphore-compatible primitive
condition/event-compatible primitive
futex-style wait/wake primitive
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

## 9.4 App supervisor

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
write lifecycle logs
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
  "memoryLimitBytes": 268435456,
  "allowMetadata": false
}
```

For the first version, each app should listen on a distinct port:

```text
orders-api    -> 0.0.0.0:5001
billing-api   -> 0.0.0.0:5002
inventory-api -> 0.0.0.0:5003
```

AWS security groups, Elastic Load Balancers, or target groups can expose selected ports externally.

---

# 10. ASP.NET Minimal API runtime contract

## 10.1 Recommended minimum: Native AOT app binaries

Use Native AOT first.

Microsoft documents Native AOT apps as self-contained, ahead-of-time compiled, and not requiring the .NET runtime to be installed on the target machine. ([Microsoft Learn][14]) Microsoft also documents ASP.NET Core Native AOT support for Minimal APIs, with limitations: not every ASP.NET Core feature is supported, MVC is unsupported, and Session is unsupported. ([Microsoft Learn][15])

Target model:

```text
Each ASP.NET Minimal API app is published as a Native AOT executable.
The OS loads each app as a separate process.
The app uses the OS ABI for memory, threads, sockets, time, files, entropy, and process control.
```

## 10.2 Critical custom OS ABI note

Publishing a normal `linux-x64` Native AOT binary assumes Linux-compatible OS services.

For your custom OS, agents must choose one of these paths:

```text
Path A:
  Implement enough Linux-compatible syscall/ABI behavior
  to run linux-x64 Native AOT binaries.

Path B:
  Port .NET Native AOT / runtime PAL to the custom OS
  and publish apps for a custom OS target.

Path C:
  Provide a compatibility shim that maps expected runtime calls
  to your custom OS syscalls.
```

Recommended first path:

```text
Implement a narrow Linux-like ABI surface only for Native AOT ASP.NET apps.
Do not implement full Linux.
Do not implement full POSIX.
```

## 10.3 Larger path: CoreCLR/JIT runtime

This is **not** the first target.

Only choose this if the OS must run ordinary framework-dependent ASP.NET apps.

Additional requirements:

```text
dynamic loader
shared libraries
JIT executable memory
mmap / mprotect behavior
thread suspension
GC safepoint support
exception unwinding
assembly loading
dependency resolution
runtimeconfig/deps handling
larger filesystem surface
more complete libc/POSIX behavior
```

---

# 11. Minimal OS ABI for ASP.NET Minimal API apps

Whether the OS exposes Linux-like syscalls or a custom ABI, ASP.NET apps need the following.

## 11.1 Process and environment

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

## 11.2 Memory

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

## 11.3 Threads and synchronization

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

## 11.4 Filesystem

For a baked-AMI first version, keep this small.

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
read-only /apps
read-only /etc/os-apps
writable /var/log
writable /tmp
optional writable /data on attached EBS volume
```

Minimum paths:

```text
/apps
/etc/os-apps
/var/log
/tmp
/data
```

## 11.5 Networking and sockets

Kestrel needs socket support.

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

or a completion-port-style equivalent.

The .NET/Kestrel port must be able to express:

```text
many concurrent connections
nonblocking accept
nonblocking read/write
timers
cancellation
backpressure
connection close notification
```

## 11.6 Entropy and crypto

Required:

```text
secure random bytes
```

Minimum API:

```text
getrandom(buffer)
```

Needed by:

```text
.NET runtime
TLS
auth tokens
GUIDs
cryptographic libraries
```

## 11.7 Time

Required:

```text
monotonic time
wall-clock time
timer queue
timezone optional
```

Minimum APIs:

```text
clock_gettime_monotonic
clock_gettime_utc
timer_wait
```

AWS DHCP option sets can provide Amazon Time Sync configuration. ([AWS Documentation][11])

---

# 12. HTTP, HTTPS, and TLS

For the absolute minimum, run apps over HTTP inside the EC2 instance or private VPC:

```text
http://0.0.0.0:5001
http://0.0.0.0:5002
```

Terminate HTTPS at one of:

```text
AWS Application Load Balancer
AWS Network Load Balancer with TLS listener
AWS CloudFront
external reverse proxy
separate TLS terminator service
```

Microsoft’s Native AOT Minimal API template guidance notes that the template uses HTTP only and expects HTTPS to be handled by an ingress service. ([Microsoft Learn][15])

If apps must serve HTTPS directly, add:

```text
TLS library port
certificate loading
private-key storage
certificate chain validation
system trust store
SNI
ALPN if HTTP/2 is needed
```

HTTP/1.1 is the minimum. HTTP/2, HTTP/3, gRPC, and WebSockets are separate feature tracks.

---

# 13. Logging and diagnostics

## 13.1 Serial console

The OS **MUST** write boot and panic logs to the EC2 serial console.

Minimum log events:

```text
kernel entry
memory map parsed
ACPI initialized
PCI bus enumerated
ENA initialized
NVMe initialized
DHCP lease acquired
app supervisor started
each app started
each app bound to port
panic / exception / app crash
```

AWS EC2 Serial Console provides access to the instance serial port for boot and network troubleshooting without requiring network access. ([AWS Documentation][16]) Your OPS/Nanos AWS document also supports `ops instance logs`, notes Nitro serial behavior, and recommends syslog or CloudWatch for production logs. 

## 13.2 Production logging

Do not ship high-volume application logs through serial.

Your OPS/Nanos document states that sending lots of output through the serial console degrades performance and shows how to disable serial/VGA output. 

Recommended production paths:

```text
syslog client
CloudWatch Logs client
custom log shipper
EBS-backed local logs with external collection
```

If implementing CloudWatch directly, the OS needs:

```text
HTTPS client
TLS
AWS SigV4 signing
IAM role credentials from IMDSv2
CloudWatch Logs API support
log buffering
retry/backoff
```

OPS/Nanos uses an instance profile plus CloudWatch/TLS support for CloudWatch logging. 

---

# 14. Security minimum

Because the OS runs multiple apps, it needs basic containment.

## 14.1 Required

```text
kernel/user separation
per-process address spaces
validated syscall pointers
non-executable user stacks by default
per-app memory limits
per-app port ownership
process termination on illegal memory access
metadata access control
kernel panic isolation where possible
```

## 14.2 Strongly recommended

```text
W^X memory policy
ASLR
read-only app code mappings
capability-style app permissions
per-app filesystem permissions
per-app network bind permissions
per-app IMDS permission
crash restart policy
```

## 14.3 Not required for first AWS boot

```text
Linux kernel
full POSIX
users/groups
password login
SSH
shell
sudo
package manager
containers
systemd
cloud-init
SSM agent
CloudWatch agent
```

---

# 15. AWS deployment surface

The deployment layer handles:

```text
AWS credentials
S3 bucket for image creation
vmimport role or AMI registration path
AMI
region
availability zone
VPC
subnet
security group
public IP / Elastic IP
load balancer / target group
Auto Scaling Group
EBS volumes
instance profile
```

Your OPS/Nanos AWS document shows VPC/security group selection, ports, static private IP, Elastic IP, Auto Scaling Group integration, CloudWatch instance profile, IPv6, and volumes.   

The guest OS handles:

```text
boot
device drivers
DHCP
IP address assignment
default route
DNS resolver
TCP listeners
app supervisor
app isolation
logging
optional IMDSv2
optional CloudWatch
optional IPv6
optional packet forwarding
```

---

# 16. Required implementation work packages for AI agents

Use these as agent-deliverable modules.

---

## Agent A — AWS AMI and image pipeline

**Goal:** Produce an AMI that AWS can launch.

Deliverables:

```text
disk image builder
root filesystem layout
boot partition layout
bootloader integration
EBS-backed root image
AMI registration/import instructions
HVM virtualization metadata
architecture metadata
boot-mode metadata
ENA support flag where required
OPS-compatible CloudConfig mapping
```

Acceptance tests:

```text
image uploads/imports successfully
AMI is created in target Region
AMI architecture is correct
AMI virtualization type is hvm
AMI boot mode matches disk layout
EC2 instance reaches kernel entry
serial console prints "kernel started"
```

---

## Agent B — EC2 boot and kernel core

**Goal:** Bring up the kernel on EC2.

Deliverables:

```text
x86-64 long mode
firmware memory map parser
ACPI parser
page allocator
virtual memory manager
kernel heap
IDT/exceptions
APIC/interrupt controller
timer
panic logger
serial console driver
```

Acceptance tests:

```text
intentional page fault reports fault address
timer interrupt fires repeatedly
kernel can allocate/free pages
ACPI tables are parsed
panic path writes to serial
```

---

## Agent C — Nitro platform support

**Goal:** Discover Nitro hardware devices.

Deliverables:

```text
PCI/PCIe enumeration
BAR mapping
DMA-safe memory allocator
MSI/MSI-X support if needed
device registry
ENA device discovery
NVMe device discovery
```

Acceptance tests:

```text
OS logs PCI devices
OS detects ENA NIC
OS detects NVMe root volume
OS maps device BARs correctly
OS handles device interrupts
```

---

## Agent D — ENA and network stack

**Goal:** Get an AWS VPC IP and accept TCP connections.

Deliverables:

```text
ENA driver
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

## Agent E — NVMe, EBS, and filesystem

**Goal:** Load multiple app packages and write logs.

Deliverables:

```text
NVMe driver
EBS root-volume support
optional data-volume support
block cache
partition parser
minimal filesystem
read-only app area
writable log/tmp area
optional /data volume
```

Acceptance tests:

```text
OS reads app manifest from disk
OS loads app executable from disk
OS writes app logs
OS reads config file
OS survives reboot with persistent logs if writable disk enabled
```

---

## Agent F — Process and memory isolation

**Goal:** Run multiple independent apps.

Deliverables:

```text
process table
per-process address spaces
ELF/native executable loader
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

## Agent J — EC2 metadata and IAM integration

**Goal:** Optional support for user data, IAM roles, and AWS SDK compatibility.

Deliverables:

```text
HTTP client
IMDSv2 token request
metadata GET support
user-data fetch
instance identity fetch
region discovery
IAM role credential fetch
metadata access policy per app
credential cache
credential refresh
```

Acceptance tests:

```text
IMDSv2 token acquired
instance-id fetched
region fetched
user-data fetched
IAM credentials fetched when instance profile exists
app without permission cannot access metadata
app with permission receives scoped configuration
```

---

## Agent K — CloudWatch logging

**Goal:** Optional production log shipping.

Deliverables:

```text
TLS client
AWS SigV4 signing
CloudWatch Logs PutLogEvents support
log group/stream config
buffering
retry/backoff
credential refresh through IMDSv2
```

Acceptance tests:

```text
system logs appear in CloudWatch Logs
app logs appear in CloudWatch Logs
network interruption does not crash logger
credentials refresh before expiration
```

---

## Agent L — Graviton support

**Goal:** Optional ARM64 EC2 support.

Deliverables:

```text
aarch64 kernel entry
UEFI boot path
ARM page tables
GIC interrupt support
ARM generic timer
ENA on ARM64
NVMe on ARM64
AMI creation path validated for arm64
```

Acceptance tests:

```text
Graviton instance reaches kernel entry
ENA works on Graviton
NVMe root volume works on Graviton
multiple apps run under SMP
```

---

# 17. Minimum end-to-end milestone sequence

## Milestone 1 — AMI boots

```text
EC2 instance boots custom AMI
kernel writes serial logs
no networking required
```

## Milestone 2 — Nitro platform discovered

```text
ACPI initialized
PCI enumerated
ENA device discovered
NVMe device discovered
```

## Milestone 3 — Network online

```text
ENA works
DHCP succeeds
DNS configured
TCP listener accepts connection
```

## Milestone 4 — Storage online

```text
NVMe root volume readable
app manifest loaded from disk
app executable loaded from disk
logs writable
```

## Milestone 5 — Single ASP.NET app

```text
one Native AOT ASP.NET Minimal API app starts
GET / returns response
serial/app logs show lifecycle
```

## Milestone 6 — Multiple ASP.NET apps

```text
two or more ASP.NET Minimal API apps start
each binds a separate port
apps run in separate processes
one app crash does not affect others
```

## Milestone 7 — AWS operational readiness

```text
AMI deployment repeatable
security group exposes required ports
serial logs retrievable
CloudWatch/syslog optional
IMDSv2 optional
IAM credentials optional
```

## Milestone 8 — Scaling path

```text
AMI works with launch template
instance can join Auto Scaling Group
apps work behind load balancer
health endpoint responds
```

OPS/Nanos has initial support for placing an AWS instance into an Auto Scaling Group by creating a launch template and applying it to the group. 

---

# 18. Absolute minimal final checklist

For this specific OS, the minimum viable AWS EC2 target is:

```text
[AWS image]
- AMI
- EBS-backed root volume
- HVM virtualization
- x86_64 first
- legacy-bios or uefi boot mode
- boot metadata matches actual disk layout
- ENA support enabled for Nitro target
- S3/vmimport or equivalent AMI creation path

[Boot/kernel]
- x86-64 kernel
- BIOS or UEFI boot path
- paging
- ACPI
- PCI/PCIe enumeration
- interrupts
- timer
- serial console
- panic logger

[AWS Nitro devices]
- ENA network driver
- NVMe block driver
- DMA-safe buffers
- device interrupts

[Network]
- Ethernet
- ARP
- IPv4
- DHCP
- UDP
- TCP
- DNS
- ICMP recommended
- IPv6/DHCPv6 optional

[Storage]
- NVMe root volume
- partition parser
- minimal filesystem
- read-only app area
- writable logs/tmp
- optional writable data volume

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
- narrow Linux-like ABI or custom .NET Native AOT OS target
- socket API
- nonblocking async I/O primitive
- timers
- filesystem read/write
- environment variables
- secure random
- clock APIs

[AWS optional later]
- IMDSv2
- IAM role credentials
- user data
- CloudWatch logs
- SSM-like management
- IPv6
- Graviton
- Xen compatibility
- multiple ENIs
- Auto Scaling Group integration
```

---

# 19. Features not required initially

You can skip these for the first AWS version:

| Feature                  | Reason                                                           |
| ------------------------ | ---------------------------------------------------------------- |
| Linux kernel             | EC2 requires a bootable compatible guest, not Linux specifically |
| Full POSIX               | Only the .NET/app ABI surface is required                        |
| `fork()`                 | Use `spawn()`                                                    |
| Shell                    | Not required for baked app execution                             |
| SSH                      | Not required if serial logs and baked deployment work            |
| `systemd`                | Implement your own app supervisor                                |
| `cloud-init`             | Avoid with baked AMI                                             |
| AWS SSM Agent            | Optional management feature                                      |
| CloudWatch Agent         | Optional; implement direct logging later if needed               |
| Docker/container runtime | Your OS already supervises isolated app processes                |
| Xen support              | Optional compatibility track                                     |
| Graviton                 | Optional second architecture track                               |
| IPv6                     | Optional unless explicitly required                              |
| Multiple ENIs            | Optional                                                         |
| ENA Express              | Optional performance feature                                     |
| HTTPS inside app         | Terminate at AWS ingress first                                   |

---

# 20. Practical bottom line

The **absolute minimal AWS-capable version** of your custom multi-app ASP.NET OS is:

```text
HVM AMI
x86_64 boot path
ACPI + PCIe
serial console
ENA network driver
NVMe block driver
DHCP IPv4
TCP/UDP/DNS
minimal filesystem
process isolation
preemptive scheduler
thread/futex primitives
Native AOT ASP.NET Minimal API ABI
app supervisor
multiple app manifests
```

The AWS-specific architectural shift is:

```text
Azure minimum:
  Hyper-V VMBus + NetVSC + StorVSC

AWS minimum:
  PCIe + ENA + NVMe
```

The multi-app architectural requirement remains:

```text
Not a unikernel.
Not one app in one address space.

This OS must be a small application platform:
  multiple isolated processes
  multiple ASP.NET Minimal API services
  one supervisor
  one AWS-compatible EC2 device/runtime layer
```

[1]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/instance-types.html "Amazon EC2 instance types - Amazon Elastic Compute Cloud"
[2]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/AMIs.html "Amazon Machine Images in Amazon EC2 - Amazon Elastic Compute Cloud"
[3]: https://docs.aws.amazon.com/vm-import/latest/userguide/prerequisites.html "Requirements for resources that you import with VM Import/Export - VM Import/Export"
[4]: https://docs.aws.amazon.com/vm-import/latest/userguide/limitations-image-importing.html "Limitations for resources being imported with VM Import/Export - VM Import/Export"
[5]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/launch-instance-boot-mode.html "Requirements to launch an EC2 instance in UEFI boot mode - Amazon Elastic Compute Cloud"
[6]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/enhanced-networking.html "Enhanced networking on Amazon EC2 instances - Amazon Elastic Compute Cloud"
[7]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/enhanced-networking-ena.html "Enable enhanced networking with ENA on your EC2 instances - Amazon Elastic Compute Cloud"
[8]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/aws-nvme-drivers.html "AWS NVMe drivers - Amazon Elastic Compute Cloud"
[9]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/RootDeviceStorage.html "Root volumes for your Amazon EC2 instances - Amazon Elastic Compute Cloud"
[10]: https://docs.aws.amazon.com/vpc/latest/userguide/VPC_DHCP_Options.html "DHCP option sets in Amazon VPC - Amazon Virtual Private Cloud"
[11]: https://docs.aws.amazon.com/vpc/latest/userguide/DHCPOptionSetConcepts.html "DHCP option set concepts - Amazon Virtual Private Cloud"
[12]: https://docs.aws.amazon.com/vpc/latest/userguide/vpc-security-groups.html "Control traffic to your AWS resources using security groups - Amazon Virtual Private Cloud"
[13]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/instancedata-data-retrieval.html "Access instance metadata for an EC2 instance - Amazon Elastic Compute Cloud"
[14]: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ "Native AOT deployment overview - .NET | Microsoft Learn"
[15]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-10.0 "ASP.NET Core support for Native AOT | Microsoft Learn"
[16]: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-serial-console.html?utm_source=chatgpt.com "EC2 Serial Console for instances"
