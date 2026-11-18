using System.Security.Cryptography;
using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Crypto;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;
using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.State;

namespace SmartCafeteria.Licensing.Service;

public sealed class LicenseService : ILicenseService
{
    private readonly HardwareIdentity _hardware;
    private readonly ILicenseStateStore _store;
    private readonly EcdsaLicenseVerifier _verifier;
    private readonly ILicenseLog _log;
    private readonly TimeProvider _timeProvider;

    public LicenseService(
        IHardwareIdProvider hardwareProvider,
        ILicenseStateStore store,
        EcdsaLicenseVerifier verifier,
        ILicenseLog log,
        TimeProvider? timeProvider = null)
    {
        _hardware = hardwareProvider.GetIdentity();
        _store = store;
        _verifier = verifier;
        _log = log;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string DeviceId => _hardware.DeviceId;

    public Task<LicenseStatus> GetStatusAsync()
    {
        var now = _timeProvider.GetUtcNow();
        var load = _store.Load();

        if (load.Tampered)
        {
            _log.Write("License state failed authentication.");
            return Task.FromResult(LicensePolicy.Locked(
                _hardware.DeviceId,
                LockReason.StateTampered,
                "تم اكتشاف تعديل غير مصرح به في بيانات الترخيص."));
        }

        if (load.State is null)
        {
            if (!string.IsNullOrWhiteSpace(load.Error))
            {
                _log.Write($"Unable to read license state: {load.Error}");
                return Task.FromResult(LicensePolicy.Locked(
                    _hardware.DeviceId,
                    LockReason.StateCorrupt,
                    "تعذر قراءة حالة الترخيص."));
            }

            var trialState = new LicenseState(now, now, LicenseKind.None, null, _hardware.DeviceId);
            load = new LicenseStateLoadResult(trialState, false, null);

            if (!_store.Save(trialState))
            {
                _log.Write("Unable to persist first-run trial state.");
                return Task.FromResult(LicensePolicy.Locked(
                    _hardware.DeviceId,
                    LockReason.StateCorrupt,
                    "تعذر حفظ حالة الترخيص. تحقق من صلاحيات الكتابة."));
            }
        }

        var state = load.State!;
        var effectiveNow = LicensePolicy.EffectiveNow(now, state.LastSeenUtc);

        if (effectiveNow > state.LastSeenUtc)
        {
            state = state with { LastSeenUtc = effectiveNow };

            if (!_store.Save(state))
                _log.Write("Unable to refresh the license last-seen timestamp.");
        }

        return Task.FromResult(LicensePolicy.Evaluate(state, _hardware, effectiveNow));
    }

    public Task<ActivationRequest> CreateRequestAsync(LicenseKind kind)
    {
        if (kind is not (LicenseKind.Monthly or LicenseKind.Permanent))
            throw new ArgumentException("Unsupported license kind.", nameof(kind));

        var now = _timeProvider.GetUtcNow();
        var load = _store.Load();
        var state = load.State;

        if (state?.Kind == LicenseKind.Permanent)
            throw new InvalidOperationException("الجهاز مفعل بشكل دائم.");

        var effectiveNow = state is null
            ? now
            : LicensePolicy.EffectiveNow(now, state.LastSeenUtc);
        var nonce = RandomNumberGenerator.GetBytes(4);

        var info = new ActivationRequestInfo(
            kind,
            now,
            _hardware.DeviceId,
            _hardware.MacAddress,
            state?.ExpiresUtc,
            nonce,
            _hardware.IdBytes,
            _hardware.MacBytes);

        return Task.FromResult(new ActivationRequest(
            kind,
            now,
            _hardware.DeviceId,
            _hardware.MacAddress,
            state?.Kind == LicenseKind.Monthly ? state.ExpiresUtc : null,
            nonce,
            RequestCodec.Encode(info)));
    }

    public Task<ActivationResult> ActivateAsync(string code)
    {
        var now = _timeProvider.GetUtcNow();
        var verification = _verifier.Verify(code, _hardware, now);

        if (!verification.IsValid)
        {
            _log.Write($"Activation rejected: {verification.Message}");
            return Task.FromResult(ActivationResult.Fail(verification.Message));
        }

        var load = _store.Load();
        var current = load.State;

        if (current?.Kind == LicenseKind.Permanent)
            return Task.FromResult(ActivationResult.Fail("الجهاز مفعل بشكل دائم."));

        if (verification.Kind == LicenseKind.Monthly &&
            current?.Kind == LicenseKind.Monthly &&
            current.ExpiresUtc.HasValue &&
            verification.ExpiresUtc.HasValue &&
            verification.ExpiresUtc.Value <= current.ExpiresUtc.Value)
        {
            return Task.FromResult(ActivationResult.Fail(
                "رمز التفعيل الحالي لا يمدد صلاحية الترخيص الموجود."));
        }

        var firstRun = current?.FirstRunUtc ?? now;
        var lastSeen = current is null
            ? now
            : LicensePolicy.EffectiveNow(now, current.LastSeenUtc);

        var state = new LicenseState(
            firstRun,
            lastSeen,
            verification.Kind,
            verification.Kind == LicenseKind.Permanent ? null : verification.ExpiresUtc,
            _hardware.DeviceId);

        if (!_store.Save(state))
        {
            _log.Write("Activation could not be persisted.");
            return Task.FromResult(ActivationResult.Fail(
                "تعذر حفظ الترخيص. تحقق من صلاحيات الكتابة ثم حاول مجدداً."));
        }

        var status = LicensePolicy.Evaluate(state, _hardware, lastSeen);
        _log.Write($"License activated: {verification.Kind} for {_hardware.DeviceId}.");

        return Task.FromResult(new ActivationResult(
            true,
            "تم تفعيل البرنامج بنجاح.",
            status));
    }
}
