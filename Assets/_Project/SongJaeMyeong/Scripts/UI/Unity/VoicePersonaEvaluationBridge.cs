using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 팀 UI(OpenSourceTeam6.UI.Unity)와 음성/LLM 심사 컴포넌트(EmployeeVoice.VoiceMovementIntegration)를
    /// 어셈블리 순환 참조 없이 연결하는 런타임 심사 어댑터.
    /// 씬에 활성화된 VoiceMovementIntegration이 있으면 Gateway의 POST /api/v1/employee/evaluate-persona를 호출하고
    /// TryPrepareEvaluatedPersona로 채용 데이터를 생성하며, 없으면 로컬 밸런스 산식(EvaluateLocalPersonaBalance)을 사용한다.
    /// </summary>
    public sealed class VoicePersonaEvaluationBridge : IPersonaConfirmEvaluator
    {
        private MonoBehaviour cachedVoiceIntegration;
        private MethodInfo evaluateMethod;
        private MethodInfo tryPrepareMethod;
        private MethodInfo cancelMethod;
        private MethodInfo clearPendingMethod;
        private PropertyInfo isPendingProperty;

        public bool IsEvaluationPending
        {
            get
            {
                if (!TryResolveVoiceIntegration(out MonoBehaviour target) || isPendingProperty == null)
                {
                    return false;
                }

                try
                {
                    object val = isPendingProperty.GetValue(target);
                    return val is bool b && b;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<PersonaEvaluationOutcome> EvaluateAndPrepareAsync(
            string rawEmployeeName,
            string rawTraits,
            EmployeePersonaSetupData baseline,
            CancellationToken cancellationToken)
        {
            if (!TryResolveVoiceIntegration(out MonoBehaviour target) ||
                evaluateMethod == null ||
                tryPrepareMethod == null)
            {
                return EmployeePersonaPresets.EvaluateLocalPersonaBalance(rawEmployeeName, rawTraits, baseline);
            }

            try
            {
                object taskObj = evaluateMethod.Invoke(
                    target,
                    new object[] { rawEmployeeName, rawTraits, cancellationToken });
                if (!(taskObj is Task task))
                {
                    return EmployeePersonaPresets.EvaluateLocalPersonaBalance(rawEmployeeName, rawTraits, baseline);
                }

                await task.ConfigureAwait(true);
                cancellationToken.ThrowIfCancellationRequested();

                PropertyInfo resultProp = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                object evalResult = resultProp != null ? resultProp.GetValue(task) : null;
                if (evalResult == null)
                {
                    return PersonaEvaluationOutcome.Failure(
                        "이력서 심사 응답을 읽지 못했습니다. 다시 시도해 주세요.",
                        isServerUnreachable: false,
                        fallbackBaseline: baseline);
                }

                object[] prepareArgs = { baseline, evalResult, default(EmployeePersonaSetupData), string.Empty };
                object okObj = tryPrepareMethod.Invoke(target, prepareArgs);
                bool preparedOk = okObj is bool b && b;
                string rejectReason = prepareArgs[3] as string ?? "이력서 심사 결과를 적용할 수 없습니다.";
                if (!preparedOk || !(prepareArgs[2] is EmployeePersonaSetupData preparedData))
                {
                    return PersonaEvaluationOutcome.Failure(
                        rejectReason,
                        isServerUnreachable: false,
                        fallbackBaseline: baseline);
                }

                float fatigueMult = ReadFloatProperty(evalResult, "WorkFatigueMultiplier", 1.0f);
                string summaryReason = ReadStringProperty(evalResult, "SummaryReason", string.Empty);

                EmployeePersonalityProfile p = preparedData.Profile;
                EmployeePersonalityProfile withFatigue = new EmployeePersonalityProfile(
                    p.Diligence,
                    p.Laziness,
                    p.Loyalty,
                    p.Archetype,
                    p.InitialStress,
                    p.WorkStressMultiplier,
                    p.RestRecoveryMultiplier,
                    p.TaskSwitchMultiplier,
                    p.HarshToneMultiplier,
                    p.PraiseRecoveryMultiplier,
                    p.AmbiguousCommandPenalty,
                    p.WageSensitivityMultiplier,
                    p.MoveSpeedMultiplier,
                    fatigueMult);

                EmployeePersonaSetupData finalPersona = new EmployeePersonaSetupData(
                    preparedData.EmployeeName,
                    preparedData.PersonalityTraits,
                    preparedData.HourlyWageWon,
                    withFatigue,
                    summaryReason);

                return PersonaEvaluationOutcome.Success(finalPersona, fatigueMult, summaryReason);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Exception unwrapped = UnwrapException(ex);
                if (unwrapped is OperationCanceledException)
                {
                    throw unwrapped;
                }

                string msg = !string.IsNullOrWhiteSpace(unwrapped.Message)
                    ? unwrapped.Message
                    : "이력서 심사 요청에 실패했습니다. 다시 시도해 주세요.";
                bool unreachable = msg.Contains("연결할 수 없습니다") ||
                                   msg.Contains("중계서버 설정") ||
                                   msg.Contains("Unable to connect") ||
                                   msg.Contains("Connection refused");
                return PersonaEvaluationOutcome.Failure(msg, isServerUnreachable: unreachable, fallbackBaseline: baseline);
            }
        }

        public void CancelEvaluation()
        {
            if (!TryResolveVoiceIntegration(out MonoBehaviour target))
            {
                return;
            }

            try
            {
                cancelMethod?.Invoke(target, null);
                clearPendingMethod?.Invoke(target, null);
            }
            catch
            {
            }
        }

        private bool TryResolveVoiceIntegration(out MonoBehaviour target)
        {
            if (cachedVoiceIntegration != null && cachedVoiceIntegration.isActiveAndEnabled)
            {
                target = cachedVoiceIntegration;
                return true;
            }

            cachedVoiceIntegration = null;
            target = null;

            Type voiceType = Type.GetType("EmployeeVoice.VoiceMovementIntegration, Assembly-CSharp");
            if (voiceType == null)
            {
                return false;
            }

            Object found = Object.FindAnyObjectByType(voiceType);
            if (!(found is MonoBehaviour mb) || !mb.isActiveAndEnabled)
            {
                return false;
            }

            cachedVoiceIntegration = mb;
            evaluateMethod = voiceType.GetMethod("EvaluatePersonaAsync", BindingFlags.Public | BindingFlags.Instance);
            tryPrepareMethod = voiceType.GetMethod("TryPrepareEvaluatedPersona", BindingFlags.Public | BindingFlags.Instance);
            cancelMethod = voiceType.GetMethod("CancelPersonaEvaluation", BindingFlags.Public | BindingFlags.Instance);
            clearPendingMethod = voiceType.GetMethod("ClearPendingPersonaEvaluation", BindingFlags.Public | BindingFlags.Instance);
            isPendingProperty = voiceType.GetProperty("IsPersonaEvaluationPending", BindingFlags.Public | BindingFlags.Instance);

            target = cachedVoiceIntegration;
            return evaluateMethod != null && tryPrepareMethod != null;
        }

        private static Exception UnwrapException(Exception ex)
        {
            Exception current = ex;
            while (current != null)
            {
                if (current is TargetInvocationException tie && tie.InnerException != null)
                {
                    current = tie.InnerException;
                    continue;
                }

                if (current is AggregateException ae && ae.InnerExceptions.Count > 0 && ae.InnerExceptions[0] != null)
                {
                    current = ae.InnerExceptions[0];
                    continue;
                }

                break;
            }

            return current ?? ex;
        }

        private static float ReadFloatProperty(object instance, string propName, float fallback)
        {
            if (instance == null) return fallback;
            PropertyInfo prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.GetValue(instance) is float f)
            {
                return f;
            }
            return fallback;
        }

        private static string ReadStringProperty(object instance, string propName, string fallback)
        {
            if (instance == null) return fallback;
            PropertyInfo prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.GetValue(instance) is string s)
            {
                return s;
            }
            return fallback;
        }
    }
}
