package main

import (
	"cloud.google.com/go/firestore"
	"cloud.google.com/go/logging"
	"context"
	"errors"
	"fmt"
	"google.golang.org/api/iterator"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
	"log"
)

type FirestoreDb struct {
	fc                 *firestore.Client
	lc                 *logging.Client
	isConnectionClosed bool
	listeners          []*func(*SlotMachine)
}

func NewFirestoreDb(fc *firestore.Client, lc *logging.Client) (*FirestoreDb, error) {
	ctx := context.Background()
	// Verify that we can communicate and authenticate with the Firestore
	err := fc.RunTransaction(ctx, func(ctx context.Context, t *firestore.Transaction) error {
		return nil
	})
	if err != nil {
		return nil, err
	}
	return &FirestoreDb{
		fc:        fc,
		lc:        lc,
		listeners: []*func(*SlotMachine){},
	}, nil
}

func (db *FirestoreDb) Close() error {
	db.isConnectionClosed = true
	return db.fc.Close()
}

func (db *FirestoreDb) AddSlotMachine(ctx context.Context, slotMachine *SlotMachine) (id string, err error) {
	ref := db.slotMachineCollection().NewDoc()
	slotMachine.ID = ref.ID
	if _, err := ref.Create(ctx, slotMachine); err != nil {
		return "", fmt.Errorf("AddSlotMachine: %v", err)
	}
	return ref.ID, nil
}

func (db *FirestoreDb) ListSlotMachines(ctx context.Context) ([]*SlotMachine, error) {
	machines := make([]*SlotMachine, 0)
	iter := db.slotMachineCollection().Documents(ctx)
	err := db.EncodeDocuments(iter, func(machine *SlotMachine) bool {
		machines = append(machines, machine)
		return false
	})
	return machines, err
}

func (db *FirestoreDb) GetByName(ctx context.Context, name string) (*SlotMachine, error) {
	iter := db.slotMachineCollection().Where("Name", "==", name).Documents(ctx)
	var m *SlotMachine
	err := db.EncodeDocuments(iter, func(machine *SlotMachine) bool {
		m = machine
		return true
	})
	if err != nil {
		return nil, fmt.Errorf("SetTokenCount: %v", err)
	} else if m == nil {
		return nil, errors.New(NotFoundError)
	}
	return m, nil
}

func (db *FirestoreDb) GetTokenCount(ctx context.Context, id string) (int, error) {
	dr, err := db.slotMachineCollection().Doc(id).Get(ctx)
	if err != nil {
		return 0, fmt.Errorf("SetTokenCount: %v", err)
	}
	if !dr.Exists() {
		return 0, errors.New(NotFoundError)
	}
	m := &SlotMachine{}
	if err = dr.DataTo(m); err != nil {
		return 0, fmt.Errorf("SetTokenCount: %v", err)
	}
	return m.Tokens, nil
}

func (db *FirestoreDb) SetTokenCount(ctx context.Context, id string, amount int) error {
	dr, err := db.slotMachineCollection().Doc(id).Get(ctx)
	if err != nil {
		return err
	}
	if !dr.Exists() {
		return errors.New(NotFoundError)
	}
	m := &SlotMachine{Tokens: amount}
	_, err = db.slotMachineCollection().Doc(id).Set(ctx, m, firestore.Merge([]string{"Tokens"}))
	if err != nil {
		return fmt.Errorf("SetTokenCount: %v", err)
	}
	return nil
}

func (db *FirestoreDb) SetName(ctx context.Context, id string, name string) error {
	dr, err := db.slotMachineCollection().Doc(id).Get(ctx)
	if err != nil {
		return err
	}
	if !dr.Exists() {
		return errors.New(NotFoundError)
	}
	m := &SlotMachine{Name: name}
	_, err = db.slotMachineCollection().Doc(id).Set(ctx, m, firestore.Merge([]string{"Name"}))
	if err != nil {
		return fmt.Errorf("SetName: %v", err)
	}
	return nil
}

func (db *FirestoreDb) DeleteSlotMachine(ctx context.Context, id string) error {
	_, err := db.slotMachineCollection().Doc(id).Delete(ctx)
	return err
}

func (db *FirestoreDb) listenToSlotMachineChanges() {
	db.logger(logging.Info).Println("Listening to slot-machine changes")
	ctx := context.Background()
	it := db.slotMachineCollection().Snapshots(ctx)
	for !db.isConnectionClosed {
		snap, err := it.Next()
		if status.Code(err) == codes.DeadlineExceeded {
			db.logger(logging.Info).Println("Deadline exceeded")
			return
		}
		if err != nil {
			db.logger(logging.Error).Printf("Snapshot.Next: %v", err)
			return
		}
		if snap == nil {
			db.logger(logging.Info).Println("snap is nil")
			continue
		}
		err = db.EncodeDocuments(snap.Documents, func(m *SlotMachine) bool {
			for _, l := range db.listeners {
				(*l)(m)
			}
			return false
		})
		if err != nil {
			db.logger(logging.Error).Printf("Error while listening to changes: %v", err)
		}
	}
}

func (db *FirestoreDb) ListenToSlotMachineChanges(f func(machine *SlotMachine)) (cancel func()) {
	db.listeners = append(db.listeners, &f)
	cancel = func() {
		i := -1
		for i = range db.listeners {
			if db.listeners[i] == &f {
				break
			}
		}
		if i == -1 {
			return
		}
		db.listeners[i] = db.listeners[len(db.listeners)]
		db.listeners = db.listeners[:len(db.listeners)-1]
	}
	return cancel
}

func (db *FirestoreDb) EncodeDocuments(i *firestore.DocumentIterator, f func(*SlotMachine) bool) error {
	defer i.Stop()
	for {
		doc, err := i.Next()
		if err == iterator.Done {
			return nil
		}
		if err != nil {
			return err
		}
		m := &SlotMachine{}
		err = doc.DataTo(m)
		if err != nil {
			db.logger(logging.Error).Printf("Error while decoding document: %v", err)
		}
		if f(m) {
			return nil
		}
	}
}

func(db *FirestoreDb) slotMachineCollection() *firestore.CollectionRef {
	return db.fc.Collection("slotMachines")
}

func (db *FirestoreDb) logger(s logging.Severity) *log.Logger {
	return db.lc.Logger("db").StandardLogger(s)
}
