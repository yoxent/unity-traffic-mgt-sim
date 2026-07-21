#if HAS_TIMELINE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityMcpPro
{
    public class TimelineCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_timeline", CreateTimeline);
            router.Register("add_timeline_track", AddTimelineTrack);
            router.Register("add_timeline_clip", AddTimelineClip);
            router.Register("get_timeline_info", GetTimelineInfo);
            router.Register("bind_timeline_track", BindTimelineTrack);
        }

        private static object CreateTimeline(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_timeline");
            string path = GetStringParam(p, "path");
            string target = GetStringParam(p, "target");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            if (!path.EndsWith(".playable"))
                path += ".playable";

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timelineAsset, path);
            AssetDatabase.SaveAssets();

            string directorTarget = null;
            if (!string.IsNullOrEmpty(target))
            {
                var go = FindGameObject(target);
                var director = go.GetComponent<PlayableDirector>();
                if (director == null)
                {
                    director = Undo.AddComponent<PlayableDirector>(go);
                }
                director.playableAsset = timelineAsset;
                EditorUtility.SetDirty(director);
                directorTarget = GetGameObjectPath(go);
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "directorTarget", directorTarget }
            };
        }

        private static readonly Dictionary<string, Type> TrackTypeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "Animation", typeof(AnimationTrack) },
            { "Activation", typeof(ActivationTrack) },
            { "Audio", typeof(AudioTrack) },
            { "Signal", typeof(SignalTrack) },
            { "Control", typeof(ControlTrack) }
        };

        private static object AddTimelineTrack(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_timeline_track");
            string timelinePath = GetStringParam(p, "timeline_path");
            string trackTypeStr = GetStringParam(p, "track_type");
            string trackName = GetStringParam(p, "track_name");

            if (string.IsNullOrEmpty(timelinePath))
                throw new ArgumentException("timeline_path is required");
            if (string.IsNullOrEmpty(trackTypeStr))
                throw new ArgumentException("track_type is required");

            var timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timelineAsset == null)
                throw new ArgumentException($"Timeline asset not found at: {timelinePath}");

            Type trackType = null;

            // Check built-in types first
            if (!TrackTypeMap.TryGetValue(trackTypeStr, out trackType))
            {
                // Try Cinemachine track via reflection (package may not be installed)
                if (trackTypeStr.Equals("Cinemachine", StringComparison.OrdinalIgnoreCase))
                {
                    trackType = FindCinemachineTrackType();
                    if (trackType == null)
                        throw new ArgumentException("Cinemachine track type not found. Ensure the Cinemachine package is installed.");
                }
                else
                {
                    throw new ArgumentException($"Unknown track type: {trackTypeStr}. Available: Animation, Activation, Audio, Signal, Control, Cinemachine");
                }
            }

            var track = timelineAsset.CreateTrack(trackType, null, trackName ?? trackTypeStr + " Track");

            EditorUtility.SetDirty(timelineAsset);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "timeline", timelineAsset.name },
                { "trackType", trackTypeStr },
                { "trackName", track.name },
                { "trackIndex", timelineAsset.outputTrackCount - 1 }
            };
        }

        private static Type FindCinemachineTrackType()
        {
            // Search all loaded assemblies for CinemachineTrack
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Try Cinemachine 3.x (com.unity.cinemachine)
                var type = assembly.GetType("Unity.Cinemachine.CinemachineTrack");
                if (type != null) return type;

                // Try Cinemachine 2.x
                type = assembly.GetType("Cinemachine.CinemachineTrack");
                if (type != null) return type;
            }
            return null;
        }

        private static object AddTimelineClip(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_timeline_clip");
            string timelinePath = GetStringParam(p, "timeline_path");
            int trackIndex = GetIntParam(p, "track_index");
            string clipAssetPath = GetStringParam(p, "clip_asset_path");
            double startTime = (double)GetFloatParam(p, "start_time", 0f);
            double duration = (double)GetFloatParam(p, "duration", 1f);

            if (string.IsNullOrEmpty(timelinePath))
                throw new ArgumentException("timeline_path is required");

            var timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timelineAsset == null)
                throw new ArgumentException($"Timeline asset not found at: {timelinePath}");

            var tracks = timelineAsset.GetOutputTracks().ToArray();
            if (trackIndex < 0 || trackIndex >= tracks.Length)
                throw new ArgumentException($"Track index {trackIndex} out of range (0-{tracks.Length - 1})");

            var track = tracks[trackIndex];
            TimelineClip timelineClip = null;

            if (!string.IsNullOrEmpty(clipAssetPath))
            {
                // Try to load as AnimationClip
                var animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipAssetPath);
                if (animClip != null && track is AnimationTrack animTrack)
                {
                    timelineClip = animTrack.CreateClip(animClip);
                }
                // Try to load as AudioClip
                else if (track is AudioTrack audioTrack)
                {
                    var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipAssetPath);
                    if (audioClip != null)
                    {
                        timelineClip = audioTrack.CreateClip<AudioPlayableAsset>();
                        var audioAsset = timelineClip.asset as AudioPlayableAsset;
                        if (audioAsset != null)
                        {
                            var so = new SerializedObject(audioAsset);
                            var clipProp = so.FindProperty("m_Clip");
                            if (clipProp != null)
                            {
                                clipProp.objectReferenceValue = audioClip;
                                so.ApplyModifiedProperties();
                            }
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Audio clip not found at: {clipAssetPath}");
                    }
                }
                else if (animClip == null)
                {
                    throw new ArgumentException($"Clip asset not found at: {clipAssetPath}");
                }
                else
                {
                    // Generic: create default clip on the track
                    timelineClip = track.CreateDefaultClip();
                }
            }
            else
            {
                // No clip asset specified - create a default clip
                if (track is ActivationTrack)
                {
                    timelineClip = track.CreateDefaultClip();
                }
                else
                {
                    timelineClip = track.CreateDefaultClip();
                }
            }

            if (timelineClip != null)
            {
                timelineClip.start = startTime;
                timelineClip.duration = duration;
            }

            EditorUtility.SetDirty(timelineAsset);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "timeline", timelineAsset.name },
                { "trackIndex", trackIndex },
                { "trackName", track.name },
                { "clipName", timelineClip != null ? timelineClip.displayName : null },
                { "startTime", startTime },
                { "duration", duration }
            };
        }

        private static object GetTimelineInfo(Dictionary<string, object> p)
        {
            string timelinePath = GetStringParam(p, "timeline_path");

            if (string.IsNullOrEmpty(timelinePath))
                throw new ArgumentException("timeline_path is required");

            var timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timelineAsset == null)
                throw new ArgumentException($"Timeline asset not found at: {timelinePath}");

            var tracks = timelineAsset.GetOutputTracks().ToArray();
            var trackInfoList = new List<object>();

            for (int i = 0; i < tracks.Length; i++)
            {
                var track = tracks[i];
                var clips = track.GetClips().ToArray();
                var clipInfoList = new List<object>();

                foreach (var clip in clips)
                {
                    clipInfoList.Add(new Dictionary<string, object>
                    {
                        { "displayName", clip.displayName },
                        { "start", clip.start },
                        { "duration", clip.duration },
                        { "end", clip.end },
                        { "clipAssetType", clip.asset != null ? clip.asset.GetType().Name : null }
                    });
                }

                trackInfoList.Add(new Dictionary<string, object>
                {
                    { "index", i },
                    { "name", track.name },
                    { "type", track.GetType().Name },
                    { "muted", track.muted },
                    { "locked", track.locked },
                    { "clips", clipInfoList },
                    { "clipCount", clips.Length },
                    { "hasBinding", track.outputs.Any() }
                });
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", timelineAsset.name },
                { "path", timelinePath },
                { "duration", timelineAsset.duration },
                { "outputTrackCount", timelineAsset.outputTrackCount },
                { "tracks", trackInfoList }
            };
        }

        private static object BindTimelineTrack(Dictionary<string, object> p)
        {
            ThrowIfPlaying("bind_timeline_track");
            string directorTarget = GetStringParam(p, "director_target");
            string timelinePath = GetStringParam(p, "timeline_path");
            int trackIndex = GetIntParam(p, "track_index");
            string bindTarget = GetStringParam(p, "bind_target");

            if (string.IsNullOrEmpty(directorTarget))
                throw new ArgumentException("director_target is required");
            if (string.IsNullOrEmpty(timelinePath))
                throw new ArgumentException("timeline_path is required");
            if (string.IsNullOrEmpty(bindTarget))
                throw new ArgumentException("bind_target is required");

            var directorGo = FindGameObject(directorTarget);
            var director = directorGo.GetComponent<PlayableDirector>();
            if (director == null)
                throw new ArgumentException($"PlayableDirector not found on: {directorTarget}");

            var timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timelineAsset == null)
                throw new ArgumentException($"Timeline asset not found at: {timelinePath}");

            // Ensure the director is using this timeline
            if (director.playableAsset != timelineAsset)
            {
                director.playableAsset = timelineAsset;
            }

            var tracks = timelineAsset.GetOutputTracks().ToArray();
            if (trackIndex < 0 || trackIndex >= tracks.Length)
                throw new ArgumentException($"Track index {trackIndex} out of range (0-{tracks.Length - 1})");

            var track = tracks[trackIndex];
            var bindGo = FindGameObject(bindTarget);

            // Determine what to bind based on track type
            UnityEngine.Object bindObj = bindGo;
            if (track is AnimationTrack)
            {
                var animator = bindGo.GetComponent<Animator>();
                if (animator != null)
                    bindObj = animator;
            }
            else if (track is AudioTrack)
            {
                var audioSource = bindGo.GetComponent<AudioSource>();
                if (audioSource != null)
                    bindObj = audioSource;
            }

            RecordUndo(director, "Bind Timeline Track");
            director.SetGenericBinding(track, bindObj);
            EditorUtility.SetDirty(director);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "director", GetGameObjectPath(directorGo) },
                { "timeline", timelineAsset.name },
                { "trackIndex", trackIndex },
                { "trackName", track.name },
                { "trackType", track.GetType().Name },
                { "boundTo", GetGameObjectPath(bindGo) },
                { "boundObjectType", bindObj.GetType().Name }
            };
        }
    }
}
#else
using System.Collections.Generic;

namespace UnityMcpPro
{
    public class TimelineCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_timeline", NotAvailable);
            router.Register("add_timeline_track", NotAvailable);
            router.Register("add_timeline_clip", NotAvailable);
            router.Register("get_timeline_info", NotAvailable);
            router.Register("bind_timeline_track", NotAvailable);
        }

        private static object NotAvailable(Dictionary<string, object> p)
        {
            throw new System.InvalidOperationException(
                "Timeline tools require the com.unity.timeline package. " +
                "Install it via Window > Package Manager, then add 'HAS_TIMELINE' to Scripting Define Symbols in Project Settings > Player.");
        }
    }
}
#endif
