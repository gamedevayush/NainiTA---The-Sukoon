// The app's physical footprint: the very number iOS compares with its per-app limit before it kills the app
// (24 Nov 2026: "exceeded mem limit: ActiveHard 3072 MB"). MemoryLog.cs prints it to the device log.
#include <mach/mach.h>

extern "C" long long _NainitalFootprintBytes()
{
    task_vm_info_data_t info;
    mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
    if (task_info(mach_task_self(), TASK_VM_INFO, (task_info_t)&info, &count) != KERN_SUCCESS) return -1;
    return (long long)info.phys_footprint;
}
