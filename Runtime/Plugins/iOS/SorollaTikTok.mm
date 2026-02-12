#import <Foundation/Foundation.h>

// Bridge TikTokBusinessSDK via NSClassFromString to avoid hard-link.
// If the SDK pod is missing, calls gracefully no-op with a log warning.

static BOOL _tiktokAvailable = NO;

static Class _GetTikTokClass(NSString *name) {
    Class cls = NSClassFromString(name);
    if (!cls) {
        NSLog(@"[Palette:TikTok] Class %@ not found - TikTokBusinessSDK not linked?", name);
    }
    return cls;
}

/// Helper: create a TikTokBaseEvent via reflection and track it with trackTTEvent:
static void _TrackTTEvent(NSString *eventName, NSDictionary *properties) {
    if (!_tiktokAvailable) return;

    Class eventClass = _GetTikTokClass(@"TikTokBaseEvent");
    if (!eventClass) return;

    // TikTokBaseEvent *event = [[TikTokBaseEvent alloc] initWithEventName:eventName]
    SEL initEventSel = NSSelectorFromString(@"initWithEventName:");
    if (![eventClass instancesRespondToSelector:initEventSel]) {
        NSLog(@"[Palette:TikTok] TikTokBaseEvent missing initWithEventName:");
        return;
    }
    id event = [[eventClass alloc] performSelector:initEventSel withObject:eventName];
    if (!event) return;

    // Add properties via addPropertyWithKey:value:
    if (properties) {
        SEL addPropSel = NSSelectorFromString(@"addPropertyWithKey:value:");
        if ([event respondsToSelector:addPropSel]) {
            for (NSString *key in properties) {
                NSMethodSignature *sig = [event methodSignatureForSelector:addPropSel];
                NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
                [inv setSelector:addPropSel];
                NSString *k = key;
                id v = properties[key];
                [inv setArgument:&k atIndex:2];
                [inv setArgument:&v atIndex:3];
                [inv invokeWithTarget:event];
            }
        }
    }

    // [TikTokBusiness trackTTEvent:event]
    Class bizClass = _GetTikTokClass(@"TikTokBusiness");
    if (!bizClass) return;

    SEL trackSel = NSSelectorFromString(@"trackTTEvent:");
    if ([bizClass respondsToSelector:trackSel]) {
        [bizClass performSelector:trackSel withObject:event];
    } else {
        NSLog(@"[Palette:TikTok] TikTokBusiness missing trackTTEvent:");
    }
}

extern "C" {

void _SorollaTikTok_Initialize(const char* appId, const char* tiktokAppId, const char* accessToken, BOOL debugBuild) {
    NSString *appIdStr = [NSString stringWithUTF8String:appId];
    NSString *tiktokAppIdStr = [NSString stringWithUTF8String:tiktokAppId];
    NSString *accessTokenStr = [NSString stringWithUTF8String:accessToken];

    Class configClass = _GetTikTokClass(@"TikTokConfig");
    if (!configClass) return;

    // TikTokConfig *config = [TikTokConfig configWithAccessToken:appId:tiktokAppId:]
    SEL factorySel = NSSelectorFromString(@"configWithAccessToken:appId:tiktokAppId:");
    if (![configClass respondsToSelector:factorySel]) {
        NSLog(@"[Palette:TikTok] TikTokConfig missing configWithAccessToken:appId:tiktokAppId:");
        return;
    }

    NSMethodSignature *sig = [configClass methodSignatureForSelector:factorySel];
    NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
    [inv setSelector:factorySel];
    [inv setArgument:&accessTokenStr atIndex:2];
    [inv setArgument:&appIdStr atIndex:3];
    [inv setArgument:&tiktokAppIdStr atIndex:4];
    [inv invokeWithTarget:configClass];

    __unsafe_unretained id config = nil;
    [inv getReturnValue:&config];

    if (!config) {
        NSLog(@"[Palette:TikTok] Failed to create TikTokConfig");
        return;
    }

    // [config setAutoStart:YES] — enable auto-tracking
    SEL autoStartSel = NSSelectorFromString(@"setAutoStart:");
    if ([config respondsToSelector:autoStartSel]) {
        NSInvocation *autoInv = [NSInvocation invocationWithMethodSignature:[config methodSignatureForSelector:autoStartSel]];
        [autoInv setSelector:autoStartSel];
        BOOL yes = YES;
        [autoInv setArgument:&yes atIndex:2];
        [autoInv invokeWithTarget:config];
    }

    // Debug mode + verbose logging for development builds
    if (debugBuild) {
        SEL debugSel = NSSelectorFromString(@"enableDebugMode");
        if ([config respondsToSelector:debugSel])
            [config performSelector:debugSel];

        SEL logLevelSel = NSSelectorFromString(@"setLogLevel:");
        if ([config respondsToSelector:logLevelSel]) {
            NSInteger debugLevel = 1; // TikTokLogLevelDebug
            NSInvocation *logInv = [NSInvocation invocationWithMethodSignature:
                [config methodSignatureForSelector:logLevelSel]];
            [logInv setSelector:logLevelSel];
            [logInv setArgument:&debugLevel atIndex:2];
            [logInv invokeWithTarget:config];
        }
    }

    // [TikTokBusiness initializeSdk:config]
    Class bizClass = _GetTikTokClass(@"TikTokBusiness");
    if (!bizClass) return;

    SEL initSdkSel = NSSelectorFromString(@"initializeSdk:");
    if ([bizClass respondsToSelector:initSdkSel]) {
        [bizClass performSelector:initSdkSel withObject:config];
        _tiktokAvailable = YES;
        NSLog(@"[Palette:TikTok] iOS SDK initialized%s", debugBuild ? " (debug)" : "");
    } else {
        NSLog(@"[Palette:TikTok] TikTokBusiness missing initializeSdk:");
    }
}

void _SorollaTikTok_TrackEvent(const char* eventName) {
    NSString *name = [NSString stringWithUTF8String:eventName];
    _TrackTTEvent(name, nil);
}

void _SorollaTikTok_TrackPurchase(double value, const char* currency) {
    NSDictionary *props = @{
        @"value": [NSString stringWithFormat:@"%.6f", value],
        @"currency": [NSString stringWithUTF8String:currency]
    };
    _TrackTTEvent(@"Purchase", props);
}

void _SorollaTikTok_TrackAdRevenue(double value, const char* currency, const char* networkName) {
    if (!_tiktokAvailable) return;

    // Use dedicated TikTokAdRevenueEvent (SDK >= 1.5.0) for proper ad revenue schema
    Class adRevClass = _GetTikTokClass(@"TikTokAdRevenueEvent");
    if (!adRevClass) {
        // Fallback to base event if class not found (SDK < 1.5.0)
        NSDictionary *props = @{
            @"value": [NSString stringWithFormat:@"%.6f", value],
            @"currency": [NSString stringWithUTF8String:currency]
        };
        _TrackTTEvent(@"ImpressionLevelAdRevenue", props);
        return;
    }

    NSMutableDictionary *adRevenue = [NSMutableDictionary dictionary];
    [adRevenue setObject:@(value) forKey:@"revenue"];
    [adRevenue setObject:[NSString stringWithUTF8String:currency] forKey:@"currency"];
    if (networkName) {
        [adRevenue setObject:@"applovin_max_sdk" forKey:@"device_ad_mediation_platform"];
        [adRevenue setObject:[NSString stringWithUTF8String:networkName] forKey:@"network_name"];
    }

    // [[TikTokAdRevenueEvent alloc] initWithAdRevenue:adRevenue eventId:nil]
    SEL initSel = NSSelectorFromString(@"initWithAdRevenue:eventId:");
    if (![adRevClass instancesRespondToSelector:initSel]) {
        NSLog(@"[Palette:TikTok] TikTokAdRevenueEvent missing initWithAdRevenue:eventId:");
        return;
    }

    NSMethodSignature *sig = [adRevClass instanceMethodSignatureForSelector:initSel];
    NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
    [inv setSelector:initSel];
    NSMutableDictionary *dict = adRevenue;
    id nilEventId = nil;
    [inv setArgument:&dict atIndex:2];
    [inv setArgument:&nilEventId atIndex:3];
    [inv invokeWithTarget:[adRevClass alloc]];

    __unsafe_unretained id event = nil;
    [inv getReturnValue:&event];
    if (!event) return;

    // [TikTokBusiness trackTTEvent:event]
    Class bizClass = _GetTikTokClass(@"TikTokBusiness");
    SEL trackSel = NSSelectorFromString(@"trackTTEvent:");
    if (bizClass && [bizClass respondsToSelector:trackSel])
        [bizClass performSelector:trackSel withObject:event];
}

} // extern "C"
