// Haptics on the iPhone (Haptics.cs): the Taptic Engine's own light, medium and heavy taps, the selection tick and the
// success / warning / error patterns, instead of Unity's one long buzz (Handheld.Vibrate).
#import <UIKit/UIKit.h>

extern "C" void _NainitalHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind)
        {
            case 0: { UISelectionFeedbackGenerator* g = [[UISelectionFeedbackGenerator alloc] init]; [g selectionChanged]; break; }
            case 1: { UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [g impactOccurred]; break; }
            case 2: { UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium]; [g impactOccurred]; break; }
            case 3: { UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy]; [g impactOccurred]; break; }
            case 4: { UINotificationFeedbackGenerator* g = [[UINotificationFeedbackGenerator alloc] init]; [g notificationOccurred:UINotificationFeedbackTypeSuccess]; break; }
            case 5: { UINotificationFeedbackGenerator* g = [[UINotificationFeedbackGenerator alloc] init]; [g notificationOccurred:UINotificationFeedbackTypeWarning]; break; }
            case 6: { UINotificationFeedbackGenerator* g = [[UINotificationFeedbackGenerator alloc] init]; [g notificationOccurred:UINotificationFeedbackTypeError]; break; }
            case 7: { UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft]; [g impactOccurred]; break; }
            case 8: { UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleRigid]; [g impactOccurred]; break; }
        }
    });
}
