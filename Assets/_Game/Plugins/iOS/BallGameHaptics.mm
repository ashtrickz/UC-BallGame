#import <UIKit/UIKit.h>

// Short taps of the Taptic Engine for game feedback (Haptics.cs). style: 0 light, 1 medium, 2 heavy, 3 success.
extern "C" void PTHaptic(int style) {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (style == 3) {
            UINotificationFeedbackGenerator *n = [[UINotificationFeedbackGenerator alloc] init];
            [n prepare];
            [n notificationOccurred:UINotificationFeedbackTypeSuccess];
            return;
        }
        UIImpactFeedbackStyle s = style == 2 ? UIImpactFeedbackStyleHeavy : style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleLight;
        UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [g prepare];
        [g impactOccurred];
    });
}
