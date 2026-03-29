let room = null;
let dotNetRef = null;
const attachedAudioElements = new Map();
const videoPublications = new Map();
const mountedVideoElements = new Map();
const voiceParticipants = new Map();
const activeSpeakerIds = new Set();
let screenShareTracks = [];
let isDeafened = false;
let audioContext = null;
let audioResumeHandlersRegistered = false;
const volumeStoragePrefix = "relaychat.voice.volume";
const screenShareCaptureOptions = {
    audio: true,
    contentHint: "detail",
    preferCurrentTab: true,
    selfBrowserSurface: "exclude",
    surfaceSwitching: "include",
    systemAudio: "include",
    video: {
        frameRate: {
            ideal: 60,
            max: 60
        }
    }
};
const screenSharePublishOptions = {
    source: "screen_share",
    simulcast: true,
    degradationPreference: "maintain-resolution",
    screenShareEncoding: {
        maxBitrate: 16_000_000,
        maxFramerate: 60
    }
};

function getAudioRoot() {
    let root = document.getElementById("livekit-audio-root");
    if (root) {
        return root;
    }

    root = document.createElement("div");
    root.id = "livekit-audio-root";
    root.style.display = "none";
    document.body.appendChild(root);
    return root;
}

function getAudioTrackKey(publication, participant) {
    return publication?.trackSid ?? publication?.sid ?? participant?.identity ?? crypto.randomUUID();
}

function getParticipantIdentity(participant) {
    return participant?.identity ?? room?.localParticipant?.identity ?? null;
}

function normalizeAudioSource(track, publication) {
    const source = publication?.source ?? track?.source ?? null;
    if (source === window.LivekitClient?.Track?.Source?.ScreenShareAudio || source === "screen_share_audio") {
        return "screen";
    }

    return "voice";
}

function normalizeVideoSource(track, publication) {
    const source = publication?.source ?? track?.source ?? null;
    if (source === window.LivekitClient?.Track?.Source?.Camera || source === "camera") {
        return "camera";
    }

    if (source === window.LivekitClient?.Track?.Source?.ScreenShare || source === "screen_share") {
        return "screen";
    }

    return null;
}

function buildScreenShareCaptureOptions(includeAudio) {
    const options = {
        ...screenShareCaptureOptions,
        audio: includeAudio
    };

    const presets = window.LivekitClient?.ScreenSharePresets;
    if (presets?.original?.resolution) {
        options.resolution = presets.original.resolution;
    }

    return options;
}

function buildScreenSharePublishOptions() {
    const options = {
        ...screenSharePublishOptions
    };

    const presets = window.LivekitClient?.ScreenSharePresets;
    if (presets?.h1080fps30 && presets?.h720fps30) {
        options.screenShareSimulcastLayers = [
            {
                ...presets.h1080fps30,
                encoding: {
                    ...presets.h1080fps30.encoding,
                    maxFramerate: 60,
                    maxBitrate: Math.max(presets.h1080fps30.encoding?.maxBitrate ?? 0, 12_000_000)
                }
            },
            {
                ...presets.h720fps30,
                encoding: {
                    ...presets.h720fps30.encoding,
                    maxFramerate: 60,
                    maxBitrate: Math.max(presets.h720fps30.encoding?.maxBitrate ?? 0, 6_000_000)
                }
            }
        ];
    }

    return options;
}

function getVideoPublicationKey(identity, source) {
    return `${identity}|${source}`;
}

function getVolumeStorageKey(identity, source) {
    return `${volumeStoragePrefix}.${identity}.${source}`;
}

function clampVolumePercent(value) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return 100;
    }

    return Math.max(0, Math.min(200, Math.round(parsed)));
}

function getStoredVolume(identity, source) {
    try {
        const storedValue = localStorage.getItem(getVolumeStorageKey(identity, source));
        return storedValue === null ? 100 : clampVolumePercent(storedValue);
    } catch {
        return 100;
    }
}

function setStoredVolume(identity, source, volumePercent) {
    const normalized = clampVolumePercent(volumePercent);
    try {
        localStorage.setItem(getVolumeStorageKey(identity, source), String(normalized));
    } catch {
        // Ignore storage failures and keep the in-memory applied volume.
    }

    return normalized;
}

function getEffectiveGain(volumePercent) {
    if (isDeafened) {
        return 0;
    }

    return clampVolumePercent(volumePercent) / 100;
}

function ensureAudioContext() {
    if (audioContext) {
        return audioContext;
    }

    const Context = window.AudioContext ?? window.webkitAudioContext;
    if (!Context) {
        return null;
    }

    audioContext = new Context();
    return audioContext;
}

function resumeAudioPlayback() {
    if (audioContext && audioContext.state === "suspended") {
        audioContext.resume().catch(() => { });
    }

    for (const entry of attachedAudioElements.values()) {
        entry.element.play?.().catch(() => { });
    }

    if (room && !room.canPlaybackAudio) {
        room.startAudio().catch(() => { });
    }
}

function ensureAudioResumeHandlers() {
    if (audioResumeHandlersRegistered) {
        return;
    }

    audioResumeHandlersRegistered = true;
    const resume = () => resumeAudioPlayback();
    window.addEventListener("pointerdown", resume, { passive: true });
    window.addEventListener("keydown", resume, { passive: true });
    window.addEventListener("touchstart", resume, { passive: true });
}

function attachRemoteAudioTrack(track, publication, participant) {
    if (track.kind !== "audio") {
        return;
    }

    const key = getAudioTrackKey(publication, participant);
    if (attachedAudioElements.has(key)) {
        return;
    }

    const identity = getParticipantIdentity(participant);
    const source = normalizeAudioSource(track, publication);
    const element = track.attach();
    element.autoplay = true;
    element.playsInline = true;
    element.muted = true;
    getAudioRoot().appendChild(element);
    ensureAudioResumeHandlers();
    const volumePercent = getStoredVolume(identity, source);
    const entry = {
        element,
        track,
        identity,
        source,
        volumePercent,
        sourceNode: null,
        gainNode: null
    };

    const context = ensureAudioContext();
    if (context) {
        try {
            entry.sourceNode = context.createMediaElementSource(element);
            entry.gainNode = context.createGain();
            entry.sourceNode.connect(entry.gainNode);
            entry.gainNode.connect(context.destination);
            entry.gainNode.gain.value = getEffectiveGain(volumePercent);
            context.resume().catch(() => { });
            element.play?.().catch(() => { });
        } catch {
            element.muted = isDeafened;
            element.volume = Math.min(1, volumePercent / 100);
        }
    } else {
        element.muted = isDeafened;
        element.volume = Math.min(1, volumePercent / 100);
    }

    element.play?.().catch(() => { });

    attachedAudioElements.set(key, entry);
}

function detachRemoteAudioTrack(publication, participant) {
    const key = getAudioTrackKey(publication, participant);
    const existing = attachedAudioElements.get(key);
    if (!existing) {
        return;
    }

    try {
        existing.sourceNode?.disconnect();
        existing.gainNode?.disconnect();
    } catch {
        // Ignore Web Audio cleanup failures.
    }

    existing.track.detach(existing.element);
    existing.element.remove();
    attachedAudioElements.delete(key);
}

function detachAllRemoteAudioTracks() {
    for (const entry of attachedAudioElements.values()) {
        try {
            entry.sourceNode?.disconnect();
            entry.gainNode?.disconnect();
        } catch {
            // Ignore Web Audio cleanup failures.
        }

        const { element, track } = entry;
        track.detach(element);
        element.remove();
    }

    attachedAudioElements.clear();
}

function applyPlaybackMute() {
    for (const entry of attachedAudioElements.values()) {
        if (entry.gainNode) {
            entry.gainNode.gain.value = getEffectiveGain(entry.volumePercent);
            continue;
        }

        entry.element.muted = isDeafened;
    }
}

function setActiveSpeakerIdentities(identities) {
    activeSpeakerIds.clear();
    for (const identity of identities) {
        if (!identity) {
            continue;
        }

        activeSpeakerIds.add(identity);
    }

    if (dotNetRef) {
        dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", [...activeSpeakerIds]);
    }
}

function notifyVideoPublicationsChanged() {
    if (!dotNetRef) {
        return;
    }

    const publications = [...videoPublications.values()].map(entry => ({
        identity: entry.identity,
        source: entry.source,
        isMuted: !!entry.isMuted,
        isLocal: !!entry.isLocal
    }));
    dotNetRef.invokeMethodAsync("HandleVideoPublicationsChanged", publications);
}

function detachMountedVideo(key) {
    const existing = mountedVideoElements.get(key);
    if (!existing) {
        return;
    }

    try {
        existing.track.detach(existing.element);
    } catch {
        // Ignore detach failures while remounting video.
    }

    existing.container.replaceChildren();
    mountedVideoElements.delete(key);
}

function detachAllMountedVideos() {
    for (const key of [...mountedVideoElements.keys()]) {
        detachMountedVideo(key);
    }
}

function upsertVideoPublication(track, publication, participant, isLocal = false) {
    if (track.kind !== "video") {
        return;
    }

    const identity = getParticipantIdentity(participant);
    const source = normalizeVideoSource(track, publication);
    if (!identity || !source) {
        return;
    }

    const key = getVideoPublicationKey(identity, source);
    videoPublications.set(key, {
        key,
        identity,
        source,
        track,
        isLocal,
        isMuted: !!publication?.isMuted || !!track?.isMuted
    });
    notifyVideoPublicationsChanged();
}

function setVideoPublicationMuted(publication, participant, track, isMuted) {
    const identity = getParticipantIdentity(participant);
    const source = normalizeVideoSource(track, publication);
    if (!identity || !source) {
        return;
    }

    const key = getVideoPublicationKey(identity, source);
    const existing = videoPublications.get(key);
    if (!existing) {
        if (!isMuted && track) {
            upsertVideoPublication(track, publication, participant, identity === room?.localParticipant?.identity);
        }

        return;
    }

    existing.isMuted = isMuted;
    if (!isMuted && track) {
        existing.track = track;
    }

    notifyVideoPublicationsChanged();
}

function removeVideoPublication(publication, participant, track) {
    const identity = getParticipantIdentity(participant);
    const source = normalizeVideoSource(track, publication);
    if (!identity || !source) {
        return;
    }

    const key = getVideoPublicationKey(identity, source);
    videoPublications.delete(key);
    detachMountedVideo(key);
    notifyVideoPublicationsChanged();
}

function clearVideoPublications() {
    videoPublications.clear();
    detachAllMountedVideos();
    notifyVideoPublicationsChanged();
}

export async function syncVideoElements(slots) {
    const activeKeys = new Set();

    for (const slot of slots) {
        const key = getVideoPublicationKey(slot.identity, slot.source);
        activeKeys.add(key);

        const publication = videoPublications.get(key);
        const container = document.getElementById(slot.elementId);
        if (!container || !publication || publication.isMuted) {
            detachMountedVideo(key);
            continue;
        }

        const existing = mountedVideoElements.get(key);
        if (existing && existing.container === container) {
            continue;
        }

        detachMountedVideo(key);

        const element = publication.track.attach();
        element.autoplay = true;
        element.playsInline = true;
        element.muted = publication.isLocal;
        element.style.width = "100%";
        element.style.height = "100%";
        element.style.objectFit = publication.source === "screen" ? "contain" : "cover";
        element.style.pointerEvents = "none";
        container.replaceChildren(element);
        mountedVideoElements.set(key, {
            container,
            element,
            track: publication.track
        });
    }

    for (const key of [...mountedVideoElements.keys()]) {
        if (!activeKeys.has(key)) {
            detachMountedVideo(key);
        }
    }
}

export async function joinVoiceChannel(serverUrl, token, dotNetObjectReference) {
    if (!window.LivekitClient) {
        throw new Error("LiveKit client SDK is not loaded.");
    }

    await leaveVoiceChannel();
    dotNetRef = dotNetObjectReference;

    room = new window.LivekitClient.Room({
        adaptiveStream: true,
        dynacast: true
    });

    room.on(window.LivekitClient.RoomEvent.TrackSubscribed, (track, publication, participant) => {
        attachRemoteAudioTrack(track, publication, participant);
        upsertVideoPublication(track, publication, participant, false);
    });

    room.on(window.LivekitClient.RoomEvent.TrackUnsubscribed, (track, publication, participant) => {
        detachRemoteAudioTrack(publication, participant);
        removeVideoPublication(publication, participant, track);
    });

    room.on(window.LivekitClient.RoomEvent.TrackMuted, (publication, participant) => {
        setVideoPublicationMuted(publication, participant, publication?.track ?? null, true);
    });

    room.on(window.LivekitClient.RoomEvent.TrackUnmuted, (publication, participant) => {
        setVideoPublicationMuted(publication, participant, publication?.track ?? null, false);
    });

    room.on(window.LivekitClient.RoomEvent.LocalTrackUnpublished, publication => {
        removeVideoPublication(publication, room.localParticipant, publication?.track ?? null);
    });

    room.on(window.LivekitClient.RoomEvent.LocalTrackPublished, publication => {
        if (publication?.track) {
            upsertVideoPublication(publication.track, publication, room.localParticipant, true);
        }
    });

    room.on(window.LivekitClient.RoomEvent.ParticipantDisconnected, participant => {
        for (const publication of participant.trackPublications.values()) {
            detachRemoteAudioTrack(publication, participant);
            if (publication.track) {
                removeVideoPublication(publication, participant, publication.track);
            }
        }
    });

    room.on(window.LivekitClient.RoomEvent.ActiveSpeakersChanged, participants => {
        setActiveSpeakerIdentities(participants.map(participant => participant.identity));
    });

    await room.connect(serverUrl, token);
    await room.localParticipant.setMicrophoneEnabled(true);

    for (const participant of room.remoteParticipants.values()) {
        for (const publication of participant.trackPublications.values()) {
            if (publication.track) {
                attachRemoteAudioTrack(publication.track, publication, participant);
                upsertVideoPublication(publication.track, publication, participant, false);
            }
        }
    }

    if (!room.canPlaybackAudio) {
        try {
            await room.startAudio();
        } catch {
            // Browsers may still require an explicit user interaction later.
        }
    }
}

export async function leaveVoiceChannel() {
    const currentRoom = room;
    room = null;

    if (!currentRoom) {
        detachAllRemoteAudioTracks();
        clearVideoPublications();
        voiceParticipants.clear();
        activeSpeakerIds.clear();
        isDeafened = false;
        if (dotNetRef) {
            await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
            await dotNetRef.invokeMethodAsync("HandleVideoPublicationsChanged", []);
        }
        return;
    }

    try {
        await stopScreenShare();
    } catch {
        // Ignore teardown failures during user-initiated leave.
    }

    try {
        await currentRoom.disconnect();
    } catch {
        // LiveKit can surface a client-initiated disconnect as an exception while switching rooms.
    }

    detachAllRemoteAudioTracks();
    clearVideoPublications();
    voiceParticipants.clear();
    activeSpeakerIds.clear();
    isDeafened = false;
    if (dotNetRef) {
        await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
        await dotNetRef.invokeMethodAsync("HandleVideoPublicationsChanged", []);
    }
}

export function setVoiceParticipants(participants) {
    voiceParticipants.clear();
    for (const participant of participants) {
        voiceParticipants.set(participant.userId, participant);
    }
}

export async function setVoiceMuted(isMuted) {
    if (!room) {
        return;
    }

    await room.localParticipant.setMicrophoneEnabled(!isMuted);
}

export function setVoiceDeafened(deafened) {
    isDeafened = deafened;
    applyPlaybackMute();
}

export function getParticipantVolume(identity, source) {
    return getStoredVolume(identity, source);
}

export function setParticipantVolume(identity, source, volumePercent) {
    const normalized = setStoredVolume(identity, source, volumePercent);
    for (const entry of attachedAudioElements.values()) {
        if (entry.identity !== identity || entry.source !== source) {
            continue;
        }

        entry.volumePercent = normalized;
        if (entry.gainNode) {
            entry.gainNode.gain.value = getEffectiveGain(normalized);
        } else {
            entry.element.volume = Math.min(1, normalized / 100);
            entry.element.muted = isDeafened;
        }
    }

    return normalized;
}

export async function setCameraEnabled(isEnabled) {
    if (!room) {
        return;
    }

    await room.localParticipant.setCameraEnabled(isEnabled);

    const publication = room.localParticipant.getTrackPublication(window.LivekitClient.Track.Source.Camera);
    if (!isEnabled || !publication?.track) {
        removeVideoPublication(publication, room.localParticipant, publication?.track ?? null);
        return;
    }

    upsertVideoPublication(publication.track, publication, room.localParticipant, true);
}

async function stopScreenShare() {
    if (!room || screenShareTracks.length === 0) {
        screenShareTracks = [];
        return;
    }

    for (const track of screenShareTracks) {
        try {
            await room.localParticipant.unpublishTrack(track, true);
        } catch {
            // Ignore unpublish failures during teardown.
        }

        removeVideoPublication({ source: track.source }, room.localParticipant, track);

        try {
            track.stop();
        } catch {
            // Ignore track stop failures during teardown.
        }
    }

    screenShareTracks = [];
}

export async function setScreenShareEnabled(isEnabled) {
    if (!room) {
        return {
            started: false,
            audioIncluded: false,
            fellBackToVideoOnly: false
        };
    }

    if (!isEnabled) {
        await stopScreenShare();
        return {
            started: false,
            audioIncluded: false,
            fellBackToVideoOnly: false
        };
    }

    await stopScreenShare();
    let tracks;
    let fellBackToVideoOnly = false;
    try {
        tracks = await room.localParticipant.createScreenTracks(buildScreenShareCaptureOptions(true));
    } catch {
        fellBackToVideoOnly = true;
        tracks = await room.localParticipant.createScreenTracks(buildScreenShareCaptureOptions(false));
    }

    for (const track of tracks) {
        const publication = await room.localParticipant.publishTrack(
            track,
            track.kind === "video" ? buildScreenSharePublishOptions() : undefined);
        if (track.kind === "video") {
            upsertVideoPublication(track, publication, room.localParticipant, true);
        }

        const mediaStreamTrack = track.mediaStreamTrack;
        if (mediaStreamTrack) {
            mediaStreamTrack.addEventListener("ended", async () => {
                await stopScreenShare();
            }, { once: true });
        }
    }

    screenShareTracks = tracks;
    return {
        started: true,
        audioIncluded: tracks.some(track => track.kind === "audio"),
        fellBackToVideoOnly
    };
}

export async function debugScreenCapture() {
    const results = {
        userAgent: navigator.userAgent,
        attempts: []
    };

    for (const audioEnabled of [true, false]) {
        const attempt = {
            audioRequested: audioEnabled
        };

        try {
            const stream = await navigator.mediaDevices.getDisplayMedia({
                video: true,
                audio: audioEnabled
            });

            const tracks = stream.getTracks().map(track => ({
                kind: track.kind,
                label: track.label,
                enabled: track.enabled,
                muted: track.muted,
                readyState: track.readyState,
                settings: typeof track.getSettings === "function" ? track.getSettings() : null
            }));

            attempt.success = true;
            attempt.tracks = tracks;

            for (const track of stream.getTracks()) {
                try {
                    track.stop();
                } catch {
                    // Ignore stop failures in diagnostics.
                }
            }
        } catch (error) {
            attempt.success = false;
            attempt.error = {
                name: error?.name ?? null,
                message: error?.message ?? String(error)
            };
        }

        results.attempts.push(attempt);
    }

    console.log("RelayChat screen capture debug", results);
    return results;
}
