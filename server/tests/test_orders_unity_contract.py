"""The contract between the order route and the Unity client (ADR-0028, 27 Oct 2026): the answers the route gives, written as files in tests/orders/unity_contract/. The Unity tests (SpokenOrderClientTests) parse these very files, so a change of the route's
answer that the client cannot read fails here (the file is out of date) and there (the parser). `uv run --project server python -m server.tests.test_orders_unity_contract` rewrites the files from the route's own models."""
from __future__ import annotations

import json
from pathlib import Path

from server.routers.orders import ConfirmIn, ConfirmOut, DetailsIn, HearIn, HearOut

DIR = Path(__file__).resolve().parents[2] / "tests" / "orders" / "unity_contract"
OXYGEN = [{"param": "device", "question": "Which oxygen device?", "options": [{"id": "nasal_cannula", "label": "Nasal cannula"}, {"id": "simple_mask", "label": "Simple face mask"}, {"id": "non_rebreather_mask", "label": "Non-rebreather mask"}]}]


def answers() -> dict[str, dict]:
    gemma = dict(interpreter="gemma_cloud", interpreter_model="gemma4:31b-cloud", interpreter_reported_model="gemma4:31b", interpreter_ms=812, source="model", interpretation="order")
    return {
        "hear_proposal_gemma": HearOut(status="confirm", intent="ORDER_TEST", label="CRP", read_back="Confirm: order crp?", ticket="TICKET-CRP.sig", **gemma).model_dump(),
        "hear_proposal_grammar_fallback": HearOut(status="confirm", intent="ORDER_TEST", label="CRP", read_back="Confirm: order crp?", ticket="TICKET-CRP-G.sig", source="grammar", interpreter="offline_grammar", fallback_reason="CLOUD_UNREACHABLE").model_dump(),
        "hear_needs_oxygen": HearOut(status="confirm", intent="REQUEST_MEDICATION", label="Oxygen", read_back="Confirm: request oxygen?", ticket="TICKET-O2.sig", needs=OXYGEN, **gemma).model_dump(),
        "hear_clarify": HearOut(status="clarify", read_back="CT or MRI?", options=["CT", "MRI"], **{**gemma, "interpretation": "ambiguous"}).model_dump(),
        "hear_question": HearOut(status="menu", read_back="That was a question, so I did not take it as an order. Say the order plainly or use the menu.", **{**gemma, "interpretation": "question"}).model_dump(),
        "hear_unsupported": HearOut(status="menu", read_back="The simulator has no such order. Please use the menu.", **{**gemma, "interpretation": "unsupported"}).model_dump(),
        "details_done": HearOut(status="confirm", intent="REQUEST_MEDICATION", label="Oxygen (simple face mask)", read_back="Confirm: request oxygen (simple face mask)?", ticket="TICKET-O2-SM.sig", interpreter="").model_dump(),
        "confirm_released_test": ConfirmOut(outcome="released", action={"verb": "order_investigation", "params": {"test": "crp"}}, verb="order_investigation", action_pairs=[{"k": "test", "v": "crp"}]).model_dump(),
        "confirm_released_oxygen": ConfirmOut(outcome="released", action={"verb": "request_oxygen", "params": {"device": "simple_mask"}}, verb="request_oxygen", action_pairs=[{"k": "device", "v": "simple_mask"}]).model_dump(),
        "confirm_released_no_verb": ConfirmOut(outcome="released", reason="NO_ENGINE_VERB").model_dump(),
        "confirm_cancelled": ConfirmOut(outcome="cancelled").model_dump(),
        "confirm_open": ConfirmOut(outcome="open", reason="ANSWER_NOT_UNDERSTOOD").model_dump(),
        "confirm_refused": ConfirmOut(outcome="refused", reason="TICKET_ALREADY_DECIDED").model_dump(),
    }


def write() -> None:
    DIR.mkdir(parents=True, exist_ok=True)
    for name, doc in answers().items():
        (DIR / f"{name}.json").write_text(json.dumps(doc, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def test_the_contract_files_are_what_the_route_answers():
    for name, doc in answers().items():
        assert json.loads((DIR / f"{name}.json").read_text(encoding="utf-8")) == doc, f"{name}.json is out of date: run python -m server.tests.test_orders_unity_contract"
    assert {p.stem for p in DIR.glob("*.json")} == set(answers())


def test_the_bodies_unity_sends_are_what_the_route_accepts():
    """tests/orders/unity_contract/requests holds the exact bytes SpokenOrderClient builds (the Unity test SpokenOrderTests compares them); the route's own request models accept them."""
    req = DIR / "requests"
    assert HearIn.model_validate_json((req / "hear.json").read_text(encoding="utf-8")).text.startswith("Can I get a CRP")
    assert ConfirmIn.model_validate_json((req / "confirm.json").read_text(encoding="utf-8")).spoken == "yes"
    assert DetailsIn.model_validate_json((req / "details.json").read_text(encoding="utf-8")).choices == {"device": "simple_mask"}


if __name__ == "__main__":
    write()
    print(f"wrote {len(answers())} files to {DIR}")
