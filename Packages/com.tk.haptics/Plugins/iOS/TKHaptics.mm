#import <UIKit/UIKit.h>

// Native Taptic bridge for TK.Haptics (iOS). Called from IosHapticBackend via
// [DllImport("__Internal")]. Style/type ints match the C# HapticImpact / HapticNotification enums.
extern "C" {

    void _TKHapticImpact(int style) {
        UIImpactFeedbackStyle mapped = UIImpactFeedbackStyleMedium;
        if (style == 0) mapped = UIImpactFeedbackStyleLight;
        else if (style == 2) mapped = UIImpactFeedbackStyleHeavy;

        UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:mapped];
        [generator impactOccurred];
    }

    void _TKHapticSelection() {
        UISelectionFeedbackGenerator *generator = [[UISelectionFeedbackGenerator alloc] init];
        [generator selectionChanged];
    }

    void _TKHapticNotification(int type) {
        UINotificationFeedbackType mapped = UINotificationFeedbackTypeSuccess;
        if (type == 1) mapped = UINotificationFeedbackTypeWarning;
        else if (type == 2) mapped = UINotificationFeedbackTypeError;

        UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
        [generator notificationOccurred:mapped];
    }
}
