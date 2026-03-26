let room = null;
let dotNetRef = null;
const attachedAudioElements = new Map();
const attachedVideoElements = new Map();
let screenShareTracks = [];

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

function getTrackKey(publication, participant) {
    return publication?.trackSid ?? publication?.sid ?? participant?.identity ?? crypto.randomUUID();
}

function getVideoRoot() {
    return document.getElementById("livekit-video-grid");
}

function attachRemoteAudioTrack(track, publication, participant) {
    if (track.kind !== "audio") {
        return;
    }

    const key = getTrackKey(publication, participant);
    if (attachedAudioElements.has(key)) {
        return;
    }

    const element = track.attach();
    element.autoplay = true;
    element.muted = false;
    element.playsInline = true;
    getAudioRoot().appendChild(element);
    attachedAudioElements.set(key, { element, track });
}

function detachRemoteAudioTrack(publication, participant) {
    const key = getTrackKey(publication, participant);
    const existing = attachedAudioElements.get(key);
    if (!existing) {
        return;
    }

    existing.track.detach(existing.element);
    existing.element.remove();
    attachedAudioElements.delete(key);
}

function detachAllRemoteAudioTracks() {
    for (const { element, track } of attachedAudioElements.values()) {
        track.detach(element);
        element.remove();
    }

    attachedAudioElements.clear();
}

function createVideoWrapper(label) {
    const wrapper = document.createElement("div");
    wrapper.style.position = "relative";
    wrapper.style.borderRadius = "16px";
    wrapper.style.overflow = "hidden";
    wrapper.style.background = "#101418";
    wrapper.style.aspectRatio = "16 / 9";
    wrapper.style.minHeight = "180px";

    const badge = document.createElement("div");
    badge.textContent = label;
    badge.style.position = "absolute";
    badge.style.left = "12px";
    badge.style.bottom = "12px";
    badge.style.padding = "4px 8px";
    badge.style.borderRadius = "999px";
    badge.style.background = "rgba(0, 0, 0, 0.6)";
    badge.style.color = "#fff";
    badge.style.fontSize = "12px";
    badge.style.zIndex = "1";

    wrapper.appendChild(badge);
    return wrapper;
}

function notifyVideoParticipantsChanged() {
    if (!dotNetRef) {
        return;
    }

    const identities = [...new Set([...attachedVideoElements.values()].map(entry => entry.identity).filter(identity => !!identity))];
    dotNetRef.invokeMethodAsync("HandleVideoParticipantsChanged", identities);
}

function attachVideoTrack(track, publication, participant) {
    if (track.kind !== "video") {
        return;
    }

    const root = getVideoRoot();
    if (!root) {
        return;
    }

    const key = getTrackKey(publication, participant);
    if (attachedVideoElements.has(key)) {
        return;
    }

    const label = participant?.name || participant?.identity || "Video";
    const wrapper = createVideoWrapper(label);
    const element = track.attach();
    element.autoplay = true;
    element.muted = participant?.isLocal ?? false;
    element.playsInline = true;
    element.style.width = "100%";
    element.style.height = "100%";
    element.style.objectFit = "cover";

    wrapper.prepend(element);
    root.appendChild(wrapper);
    attachedVideoElements.set(key, { element, track, wrapper, identity: participant?.identity ?? null });
    notifyVideoParticipantsChanged();
}

function detachVideoTrack(publication, participant) {
    const key = getTrackKey(publication, participant);
    const existing = attachedVideoElements.get(key);
    if (!existing) {
        return;
    }

    existing.track.detach(existing.element);
    existing.wrapper.remove();
    attachedVideoElements.delete(key);
    notifyVideoParticipantsChanged();
}

function detachAllVideoTracks() {
    for (const { element, track, wrapper } of attachedVideoElements.values()) {
        track.detach(element);
        wrapper.remove();
    }

    attachedVideoElements.clear();
    notifyVideoParticipantsChanged();
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
        attachVideoTrack(track, publication, participant);
    });

    room.on(window.LivekitClient.RoomEvent.TrackUnsubscribed, (_track, publication, participant) => {
        detachRemoteAudioTrack(publication, participant);
        detachVideoTrack(publication, participant);
    });

    room.on(window.LivekitClient.RoomEvent.ParticipantDisconnected, participant => {
        for (const publication of participant.trackPublications.values()) {
            detachRemoteAudioTrack(publication, participant);
            detachVideoTrack(publication, participant);
        }
    });

    room.on(window.LivekitClient.RoomEvent.ActiveSpeakersChanged, participants => {
        if (!dotNetRef) {
            return;
        }

        const identities = participants
            .map(participant => participant.identity)
            .filter(identity => !!identity);
        dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", identities);
    });

    await room.connect(serverUrl, token);
    await room.localParticipant.setMicrophoneEnabled(true);

    for (const participant of room.remoteParticipants.values()) {
        for (const publication of participant.trackPublications.values()) {
            if (publication.track) {
                attachRemoteAudioTrack(publication.track, publication, participant);
                attachVideoTrack(publication.track, publication, participant);
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
    if (!room) {
        detachAllRemoteAudioTracks();
        detachAllVideoTracks();
        if (dotNetRef) {
            await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
            await dotNetRef.invokeMethodAsync("HandleVideoParticipantsChanged", []);
        }
        return;
    }

    await stopScreenShare();
    await room.disconnect();
    detachAllRemoteAudioTracks();
    detachAllVideoTracks();
    room = null;
    if (dotNetRef) {
        await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
        await dotNetRef.invokeMethodAsync("HandleVideoParticipantsChanged", []);
    }
}

export async function setVoiceMuted(isMuted) {
    if (!room) {
        return;
    }

    await room.localParticipant.setMicrophoneEnabled(!isMuted);
}

export async function setCameraEnabled(isEnabled) {
    if (!room) {
        return;
    }

    await room.localParticipant.setCameraEnabled(isEnabled);

    const publication = room.localParticipant.getTrackPublication(window.LivekitClient.Track.Source.Camera);
    if (!isEnabled || !publication?.track) {
        detachVideoTrack(publication, room.localParticipant);
        return;
    }

    attachVideoTrack(publication.track, publication, room.localParticipant);
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

        try {
            track.stop();
        } catch {
            // Ignore track stop failures during teardown.
        }
    }

    screenShareTracks = [];
}

export async function setScreenShareEnabled(isEnabled, shareAudio) {
    if (!room) {
        return;
    }

    if (!isEnabled) {
        await stopScreenShare();
        return;
    }

    await stopScreenShare();
    const tracks = await room.localParticipant.createScreenTracks({
        audio: shareAudio
    });

    for (const track of tracks) {
        await room.localParticipant.publishTrack(track);
    }

    screenShareTracks = tracks;
}
