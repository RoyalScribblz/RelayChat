let room = null;
let dotNetRef = null;
const attachedAudioElements = new Map();

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
    });

    room.on(window.LivekitClient.RoomEvent.TrackUnsubscribed, (_track, publication, participant) => {
        detachRemoteAudioTrack(publication, participant);
    });

    room.on(window.LivekitClient.RoomEvent.ParticipantDisconnected, participant => {
        for (const publication of participant.trackPublications.values()) {
            detachRemoteAudioTrack(publication, participant);
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
        if (dotNetRef) {
            await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
        }
        return;
    }

    await room.disconnect();
    detachAllRemoteAudioTracks();
    room = null;
    if (dotNetRef) {
        await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
    }
}

export async function setVoiceMuted(isMuted) {
    if (!room) {
        return;
    }

    await room.localParticipant.setMicrophoneEnabled(!isMuted);
}
